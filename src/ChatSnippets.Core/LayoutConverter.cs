namespace ChatSnippets.Core;

public enum LayoutDirection { ToRussian, ToLatin }

/// <summary>
/// Исправляет текст, набранный не в той раскладке: «ghbdtn» ⇄ «привет».
/// Это замена по положению клавиш ЙЦУКЕН ⇄ QWERTY, а не транслитерация.
/// </summary>
public static class LayoutConverter
{
    // Строки попарно: i-й символ английской раскладки ↔ i-й символ русской на той же клавише.
    const string Latin =
        "qwertyuiop[]asdfghjkl;'zxcvbnm,./`" +
        "QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?~" +
        "@#$^&";
    const string Russian =
        "йцукенгшщзхъфывапролджэячсмитьбю.ё" +
        "ЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,Ё" +
        "\"№;:?";

    static readonly Dictionary<char, char> ToRu = Latin.Zip(Russian).ToDictionary(p => p.First, p => p.Second);
    static readonly Dictionary<char, char> ToEn = Russian.Zip(Latin).ToDictionary(p => p.First, p => p.Second);

    /// <summary>Направление выбирается по большинству букв; без букв считаем, что нужен русский.</summary>
    public static LayoutDirection Detect(string text)
    {
        int latin = 0, cyrillic = 0;
        foreach (var c in text)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) latin++;
            else if (c >= '\u0400' && c <= '\u04FF') cyrillic++;
        }
        return latin >= cyrillic ? LayoutDirection.ToRussian : LayoutDirection.ToLatin;
    }

    public static string Convert(string text) => Convert(text, Detect(text));

    public static string Convert(string text, LayoutDirection direction)
    {
        var map = direction == LayoutDirection.ToRussian ? ToRu : ToEn;
        return string.Create(text.Length, (text, map), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var c = state.text[i];
                span[i] = state.map.TryGetValue(c, out var mapped) ? mapped : c;
            }
        });
    }
}
