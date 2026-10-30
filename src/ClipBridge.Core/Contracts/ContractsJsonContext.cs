using System.Text.Json.Serialization;

namespace ClipBridge.Core;

// Helper 프로토콜(#31)용 JSON 직렬화. byte[]는 Base64, enum은 문자열, 속성 이름은 camelCase.
// 소스 생성 방식이라 self-contained 단일 파일(트리밍)로 publish해도 동작한다
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ClipboardSnapshot))]
[JsonSerializable(typeof(ConversionOptions))]
[JsonSerializable(typeof(ConversionResult))]
[JsonSerializable(typeof(TargetApp))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
