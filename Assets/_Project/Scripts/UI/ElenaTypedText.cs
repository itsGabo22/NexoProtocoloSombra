using System.Text;

namespace Nexo.UI
{
    /// <summary>Limpieza del texto escrito a Elena (lógica pura, testeada): sin saltos de línea ni controles, espacios colapsados, máx. 140.</summary>
    public static class ElenaTypedText
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }
            string result = sb.ToString();
            return result.Length > ElenaTextInput.MaxChars ? result.Substring(0, ElenaTextInput.MaxChars) : result;
        }
    }
}
