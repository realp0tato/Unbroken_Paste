namespace ClipBridge.Windows;

// 원본을 되돌릴 때(PRD 6.5) 쓸 복사 원본의 모든 포맷 (#80). macOS는 복사한 모든 타입을 되돌린다.
// 변환에 쓰는 포맷(스냅샷)을 먼저 읽어 변환 준비를 시작하고, 나머지는 그 뒤에 읽어 원래 순서대로 합친다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class OriginalFormats
{
    // 이미지·OLE 개체처럼 큰 포맷의 상한. 넘으면 그 포맷은 되돌리지 않는다
    public const long MaxBytesPerFormat = 8L * 1024 * 1024;
    public const long MaxTotalBytes = 32L * 1024 * 1024;

    // 원본 앱 안의 OLE 개체를 가리키는 내부 포맷. 바이트를 되돌려도 원본 앱이 없으면 쓸 수 없고,
    // 대상 앱이 이것을 먼저 골라 붙여넣기가 실패할 수 있다
    private static readonly HashSet<string> Excluded = ["DataObject", "Ole Private Data"];

    // 스냅샷 다음에 읽을 포맷인지
    public static bool ShouldRead(string name, IReadOnlySet<string> snapshotFormats) =>
        !snapshotFormats.Contains(name) && !Excluded.Contains(name);

    // order: 클립보드에 있던 순서대로 (포맷 이름, 읽은 데이터 또는 null).
    // 스냅샷에 있는 포맷은 스냅샷의 데이터를 쓴다 (두 번 읽는 사이 원본 앱이 다른 데이터를 줄 수 있으므로 변환 입력과 같게).
    // 읽지 못했거나 상한을 넘는 포맷은 빼고, 빠진 이름을 skipped에 담는다
    public static IReadOnlyDictionary<string, byte[]> Merge(
        IEnumerable<(string Name, byte[]? Data)> order, IReadOnlyDictionary<string, byte[]> snapshot, out IReadOnlyList<string> skipped)
    {
        var merged = new Dictionary<string, byte[]>();
        var left = new List<string>();
        long total = snapshot.Values.Sum(data => (long)data.Length);
        foreach (var (name, data) in order)
        {
            if (merged.ContainsKey(name))
                continue;
            if (snapshot.TryGetValue(name, out var converted))
            {
                merged[name] = converted;
                continue;
            }
            if (data is null || Excluded.Contains(name) || data.Length > MaxBytesPerFormat || total + data.Length > MaxTotalBytes)
            {
                left.Add(name);
                continue;
            }
            merged[name] = data;
            total += data.Length;
        }

        // 순서 목록에 없던 스냅샷 포맷(읽는 사이 사라진 경우)도 잃지 않는다
        foreach (var (name, data) in snapshot)
            merged.TryAdd(name, data);

        skipped = left;
        return merged;
    }
}
