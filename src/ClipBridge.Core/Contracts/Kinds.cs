namespace ClipBridge.Core;

// 출처 판별 결과 (PRD 6.1)
public enum SourceKind { Excel, Web, Markdown, PlainText, Other }

// 내용 종류 (PRD 6.1)
public enum ContentKind { Table, Text }

// 붙여넣기 대상. 판별은 각 앱 껍데기가 하고 Core에는 이 값만 넘긴다 (PRD 4.5)
public enum TargetApp { Hangul, Notion, Word, Other }

// Markdown 처리 옵션 (PRD 4.2, 6.3.5)
public enum MarkdownMode { Strip, Format }
