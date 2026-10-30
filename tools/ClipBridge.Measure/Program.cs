using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using ClipBridge.Core;
using ClipBridge.Core.Tests.Fixtures;

// 성능 측정 (#24). 발표에는 이 도구로 발표 PC에서 잰 값만 쓴다 (PRD 9.1, 11)
//   bench [--runs N]  저장소 샘플마다 결과 준비(Prepare)를 N번 돌려 NFR-02(p95 200ms 이하)를 잰다
//   log <파일>        Windows 앱 로그에서 결과 준비 시간(NFR-02)과 붙여넣기 경로 시간(NFR-03: p95 20ms, 상한 50ms)을 모은다

const double PrepareP95Ms = 200, PasteP95Ms = 20, PasteMaxMs = 50;

return args switch
{
    ["bench"] => Bench(30),
    ["bench", "--runs", var n] when int.TryParse(n, out var runs) && runs > 0 => Bench(runs),
    ["log", var file] => Log(file),
    _ => Usage(),
};

static int Usage()
{
    Console.WriteLine("""
        사용법 (저장소 루트에서):
          dotnet run -c Release --project tools/ClipBridge.Measure -- bench [--runs N]
              저장소 샘플마다 결과 준비 시간을 N번(기본 30) 재서 NFR-02(p95 200ms 이하)와 비교
          dotnet run -c Release --project tools/ClipBridge.Measure -- log <로그 파일>
              Windows 앱 로그의 prepare=·elapsed= 값을 모아 NFR-02·NFR-03과 비교
        """);
    return 1;
}

static int Bench(int runs)
{
    var samples = Fixture.All().ToList();
    if (samples.Count == 0)
    {
        Console.Error.WriteLine("샘플이 없습니다 (tests/fixtures).");
        return 1;
    }

    var converter = new UpClipConverter();
    var options = ConversionOptions.Default;
    Console.WriteLine($"결과 준비 시간 (모든 대상·모드를 한 번에 준비, {runs}회) — {Environment.MachineName}, {Environment.ProcessorCount}코어, .NET {Environment.Version}");
    Console.WriteLine($"{"샘플",-28} {"글자 수",8} {"p50",8} {"p95",8} {"최대",8}  변환기");

    var all = new List<double>();
    var worst = 0.0;
    foreach (var sample in samples)
    {
        var snapshot = Fixture.Load(sample);
        // 처음 한 번은 JIT·정규식 준비가 섞이므로 재지 않는다
        using (var warmup = converter.Prepare(snapshot, options, CancellationToken.None)) { }

        var times = new List<double>(runs);
        string converters = "";
        for (var i = 0; i < runs; i++)
        {
            var started = Stopwatch.GetTimestamp();
            using var prepared = converter.Prepare(snapshot, options, CancellationToken.None);
            times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (i == 0)
                converters = string.Join(" ", Enum.GetValues<TargetApp>().Select(t => $"{t}={prepared.Get(t, options).ConverterId}"));
        }

        var p95 = Percentile(times, 95);
        worst = Math.Max(worst, p95);
        all.AddRange(times);
        Console.WriteLine($"{sample,-28} {TextLength(snapshot),8:N0} {Ms(Percentile(times, 50))} {Ms(p95)} {Ms(times.Max())}  {converters}");
    }

    Console.WriteLine();
    Console.WriteLine($"전체 {all.Count}회: p50 {Ms(Percentile(all, 50))}, p95 {Ms(Percentile(all, 95))}, 최대 {Ms(all.Max())}");
    Console.WriteLine($"NFR-02 (샘플별 p95 ≤ {PrepareP95Ms}ms): {(worst <= PrepareP95Ms ? "통과" : "미달")} — 가장 느린 샘플 p95 {Ms(worst).Trim()}");
    Console.WriteLine("1만 자를 넘는 샘플은 NFR-02 대상이 아니므로 참고로만 본다.");
    return 0;
}

static int Log(string file)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"파일이 없습니다: {file}");
        return 1;
    }

    var prepare = new List<double>();
    var paste = new Dictionary<string, List<double>>();
    foreach (var line in File.ReadLines(file))
    {
        if (PrepareTime().Match(line) is { Success: true } p)
            prepare.Add(Number(p.Groups[1].Value));
        if (PasteTime().Match(line) is { Success: true } m)
        {
            var plan = m.Groups["plan"].Value;
            if (!paste.TryGetValue(plan, out var list))
                paste[plan] = list = [];
            list.Add(Number(m.Groups["ms"].Value));
        }
    }

    Console.WriteLine($"{"구분",-28} {"횟수",6} {"p50",8} {"p95",8} {"최대",8}");
    if (prepare.Count > 0)
        Row("결과 준비 (prepare)", prepare);
    foreach (var (plan, times) in paste.OrderBy(entry => entry.Key))
        Row($"붙여넣기 plan={plan}", times);

    var pastes = paste.Values.SelectMany(times => times).ToList();
    if (pastes.Count > 0)
        Row("붙여넣기 전체", pastes);
    Console.WriteLine();

    if (prepare.Count > 0)
        Console.WriteLine($"NFR-02 (p95 ≤ {PrepareP95Ms}ms): {(Percentile(prepare, 95) <= PrepareP95Ms ? "통과" : "미달")}");
    if (pastes.Count > 0)
    {
        var ok = Percentile(pastes, 95) <= PasteP95Ms && pastes.Max() <= PasteMaxMs;
        Console.WriteLine($"NFR-03 (p95 ≤ {PasteP95Ms}ms, 최대 ≤ {PasteMaxMs}ms): {(ok ? "통과" : "미달")} — {pastes.Count(t => t > PasteMaxMs)}회가 상한 초과");
    }
    if (prepare.Count == 0 && pastes.Count == 0)
        Console.WriteLine("prepare=·elapsed= 값이 있는 줄이 없습니다. 터미널에서 Windows 앱을 실행해 남긴 로그인지 확인해 주세요.");
    return 0;

    static void Row(string label, List<double> times) =>
        Console.WriteLine($"{label,-28} {times.Count,6} {Ms(Percentile(times, 50))} {Ms(Percentile(times, 95))} {Ms(times.Max())}");
}

// 최근접 순위 방식: 값의 p%가 이 값 이하
static double Percentile(List<double> values, int p)
{
    var sorted = values.Order().ToList();
    var rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
    return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
}

static string Ms(double value) => $"{value,6:0.0}ms";

// 로그는 실행한 PC의 형식으로 숫자를 쓴다 (소수점 , 인 지역 포함)
static double Number(string text) => double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);

static int TextLength(ClipboardSnapshot snapshot) =>
    snapshot.Formats.TryGetValue(ClipFormats.WinUnicodeText, out var utf16) ? utf16.Length / 2
    : snapshot.Formats.TryGetValue(ClipFormats.MacUtf8Text, out var utf8) ? System.Text.Encoding.UTF8.GetCharCount(utf8)
    : 0;

internal static partial class Program
{
    [GeneratedRegex(@"\[convert\] results .* prepare=(\d+[.,]\d+)ms")]
    private static partial Regex PrepareTime();

    [GeneratedRegex(@"\[paste\] paste .* plan=(?<plan>\S+(?:\(clipboard busy\))?) elapsed=(?<ms>\d+[.,]\d+)ms")]
    private static partial Regex PasteTime();
}
