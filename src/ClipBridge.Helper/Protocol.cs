using System.Text.Json.Serialization;
using ClipBridge.Core;

namespace ClipBridge.Helper;

// 표준 입출력 프로토콜 메시지 (README.md). 한 줄에 JSON 하나

// 앱 → Helper. type: "prepare" | "setOptions" | "shutdown"
internal sealed record Request(string? Type, long? Id, ClipboardSnapshot? Snapshot, ConversionOptions? Options);

// Helper → 앱
internal sealed record ReadyMessage(string Type, int ProtocolVersion);

// 대상별 확정 결과. id가 null이면 AI 검증 결과(B) 도착에 따른 갱신
internal sealed record ResultsMessage(
    string Type, long? Id, long SequenceNumber, SourceKind Source, ContentKind Content,
    IReadOnlyDictionary<string, ConversionResult> Results);

internal sealed record OkMessage(string Type, long? Id);

// code: "invalid_request" | "failed"
internal sealed record ErrorMessage(string Type, long? Id, string Code, string Message);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(ReadyMessage))]
[JsonSerializable(typeof(ResultsMessage))]
[JsonSerializable(typeof(OkMessage))]
[JsonSerializable(typeof(ErrorMessage))]
internal sealed partial class HelperJsonContext : JsonSerializerContext;
