using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

public class CharacterNamesTests
{
    [Theory]
    [InlineData("Vinnie", "Vinnie")]
    [InlineData("  Rosa   Marquez  ", "Rosa Marquez")]
    [InlineData("Dominic O'Hara", "Dominic O'Hara")]
    [InlineData("Jean-Luc", "Jean-Luc")]
    [InlineData("J. Wellington", "J. Wellington")]
    [InlineData("Zoë", "Zoë")]
    [InlineData("Ana\tLuz", "Ana Luz")]
    [InlineData("Al", "Al")]
    [InlineData("Abcdefghijklmnopqrstuvwx", "Abcdefghijklmnopqrstuvwx")] // 24
    public void ValidNames_AreAccepted_AndNormalized(string raw, string expected)
    {
        Assert.Equal(expected, CharacterNames.Normalize(raw, out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void DecomposedAccent_CountsAsOneLetter()
    {
        string decomposed = "Zoë"; // e + combining diaeresis

        Assert.Equal("Zoë", CharacterNames.Normalize(decomposed, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Abcdefghijklmnopqrstuvwxy")] // 25
    [InlineData("Agent 47")]
    [InlineData("<script>")]
    [InlineData("../../etc")]
    [InlineData("Name_With_Underscores")]
    [InlineData("-Dash")]
    [InlineData("'Quote")]
    [InlineData("Emoji 🔥")]
    [InlineData("Zero​Width")]
    public void InvalidNames_AreRejected_WithAMessage(string? raw)
    {
        Assert.Null(CharacterNames.Normalize(raw, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}

public class CharacterIdsTests
{
    [Fact]
    public void NewCustomIds_AreValidCustomIds_AndUnique()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => CharacterIds.NewCustomId()).ToList();

        Assert.All(ids, id => Assert.True(CharacterIds.IsCustom(id) && CharacterIds.IsValid(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void DemoIds_AreNeverMistakenForCustomIds()
    {
        Assert.All(DemoCharacters.All, demo => Assert.False(CharacterIds.IsCustom(demo.Id)));
    }

    [Theory]
    [InlineData("char-")]
    [InlineData("char-3F9A1B2C4D5E")]
    [InlineData("char-3f9a1b2c4d5")]
    [InlineData("char-3f9a1b2c4d5e0")]
    [InlineData("char-3f9a1b2c4d5g")]
    [InlineData("../char-3f9a1b2c4d5e")]
    [InlineData("sonny-perelli")]
    public void MalformedCustomIds_AreRejected(string id)
    {
        Assert.False(CharacterIds.IsCustom(id));
    }
}
