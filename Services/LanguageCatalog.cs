namespace Cardex.Services;

public static class LanguageCatalog
{
    public static readonly (string Code, string Label)[] All =
    [
        ("EN", "English"),
        ("FR", "French"),
        ("DE", "German"),
        ("IT", "Italian"),
        ("ES", "Spanish"),
        ("JA", "Japanese"),
        ("KO", "Korean"),
        ("ZH", "Chinese"),
    ];

    public static string Label(string code) =>
        All.FirstOrDefault(l => l.Code == code).Label ?? code;
}
