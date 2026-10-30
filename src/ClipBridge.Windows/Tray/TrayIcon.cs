using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

// 이미지 파일 없이 그리는 기본 트레이 아이콘. 슬라임 애니메이션은 #44에서 바꾼다
internal static class TrayIcon
{
    private static readonly Color Fill = Color.FromArgb(0x25, 0x63, 0xEB);

    public static Icon Create(Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // 채운 원 위에 흰 글자라 밝은·어두운 작업 표시줄 모두에서 보인다
            using var fill = new SolidBrush(Fill);
            graphics.FillEllipse(fill, 0, 0, size.Width - 1, size.Height - 1);

            using var font = new Font("Segoe UI", size.Height * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("UP", font, Brushes.White, new RectangleF(0, 0, size.Width, size.Height), format);
        }

        // Clone은 핸들을 복사하므로 GetHicon이 만든 핸들은 바로 해제한다
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
