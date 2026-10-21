using System.Globalization;

namespace DFS.ProjectSyndicate.Web.Components.UI;

public static class Format
{
    public static string Money(float amount) => "$" + amount.ToString("N0", CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
