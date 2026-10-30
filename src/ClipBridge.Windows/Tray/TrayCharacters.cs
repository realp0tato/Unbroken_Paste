namespace ClipBridge.Windows;

// 트레이 캐릭터 (#44, PRD 4.2.2). macOS Resources/characters.json과 같은 목록·순서·이름
internal sealed record TrayCharacter(string Id, string Name)
{
    public const string DefaultId = "slime";

    public static IReadOnlyList<TrayCharacter> All { get; } =
    [
        new("slime", "기본 슬라임"),
        new("cat", "고양이 슬라임"),
        new("drip", "녹는 슬라임"),
        new("cloud", "구름 슬라임"),
        new("bunny", "토끼 슬라임"),
    ];

    // 저장된 값이 없거나 모르는 캐릭터면 기본 슬라임
    public static TrayCharacter Find(string? id) => All.FirstOrDefault(c => c.Id == id) ?? All[0];

    // 리소스 이름 (ClipBridge.Windows.csproj)
    public string FramePrefix => $"ClipBridge.Windows.Characters.{Id}.frame_";
    public string BadgeResource => $"ClipBridge.Windows.Characters.{Id}.badge.png";
}
