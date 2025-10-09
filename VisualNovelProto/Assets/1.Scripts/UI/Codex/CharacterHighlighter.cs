using System.Text;

public static class CharacterHighlighter
{
    static StringBuilder sb = new StringBuilder(2048);

    // 본문에서 #숫자를 <link="c:id"><color=...>이름</color></link>으로 치환
    public static string InjectLinks(string text, CharacterDatabase cdb)
    {
        if (string.IsNullOrEmpty(text) || cdb == null) return text ?? string.Empty;

        sb.Length = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '#')
            {
                int j = i + 1, id = 0; bool hasDigit = false;
                while (j < text.Length)
                {
                    int d = text[j] - '0';
                    if (d < 0 || d > 9) break;
                    hasDigit = true; id = id * 10 + d; j++;
                }

                if (hasDigit && cdb.Exists(id) && cdb.TryGetEntry(id, out var e))
                {
                    string hex = string.IsNullOrEmpty(e.colorHex) ? "#A8D8FF" : e.colorHex;
                    sb.Append("<link=\"c:");
                    sb.Append(id);
                    sb.Append("\"><color=");
                    sb.Append(hex);
                    sb.Append(">");
                    sb.Append(string.IsNullOrEmpty(e.name) ? id.ToString() : e.name);
                    sb.Append("</color></link>");
                    i = j - 1;
                    continue;
                }
            }
            sb.Append(c);
        }

        return sb.ToString();
    }
}
