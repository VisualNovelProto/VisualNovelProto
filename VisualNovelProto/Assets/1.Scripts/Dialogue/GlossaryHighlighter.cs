using System.Text;

public static class GlossaryHighlighter
{
    // 재사용 버퍼(동적 할당 최소화)
    static StringBuilder sb = new StringBuilder(2048);

    // 본문에서 &숫자 토큰을 찾아 <link> + <color> 삽입
    // 예) "이제, &0 가 시작됩니다" → <link="g:0"><color=#FFA237>클라이맥스 추리</color></link>
    public static string InjectLinks(string text, GlossaryDatabase gdb)
    {
        if (string.IsNullOrEmpty(text) || gdb == null) return text ?? string.Empty;

        sb.Length = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '&')
            {
                // & + 연속된 숫자 파싱
                int j = i + 1;
                int id = 0;
                bool hasDigit = false;

                while (j < text.Length)
                {
                    int d = text[j] - '0';
                    if (d < 0 || d > 9)
                    {
                        break;
                    }
                    hasDigit = true;
                    id = id * 10 + d;
                    j++;
                }

                if (hasDigit && gdb.Exists(id) && gdb.TryGetEntry(id, out GlossaryEntry e))
                {
                    string hex = string.IsNullOrEmpty(e.colorHex) ? "#FFD95A" : e.colorHex;
                    sb.Append("<link=\"g:");
                    sb.Append(id);
                    sb.Append("\"><color=");
                    sb.Append(hex);
                    sb.Append(">");
                    sb.Append(string.IsNullOrEmpty(e.label) ? id.ToString() : e.label);
                    sb.Append("</color></link>");
                    i = j - 1; // 소비한 만큼 인덱스 이동
                    continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
