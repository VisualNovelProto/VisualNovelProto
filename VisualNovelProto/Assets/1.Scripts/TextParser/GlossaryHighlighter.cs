using System.Text;

public static class GlossaryHighlighter
{
    // ���� ����(���� �Ҵ� �ּ�ȭ)
    static StringBuilder sb = new StringBuilder(2048);

    // �������� &���� ��ū�� ã�� <link> + <color> ����
    // ��) "����, &0 �� ���۵˴ϴ�" �� <link="g:0"><color=#FFA237>Ŭ���̸ƽ� �߸�</color></link>
    public static string InjectLinks(string text, GlossaryDatabase gdb)
    {
        if (string.IsNullOrEmpty(text) || gdb == null) return text ?? string.Empty;

        sb.Length = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '&')
            {
                // & + ���ӵ� ���� �Ľ�
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
                    i = j - 1; // �Һ��� ��ŭ �ε��� �̵�
                    continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
