using System.Globalization;

namespace DFS.ProjectSyndicate.Web.Components.UI;

public static class Format
{
    public static string Money(float amount) => "$" + amount.ToString("N0", CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    // An average such as XP per task: whole numbers plain, otherwise one decimal place ("7.5").
    public static string Rate(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
