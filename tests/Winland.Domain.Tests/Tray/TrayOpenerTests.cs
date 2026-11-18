namespace Winland.Domain.Tests;

[TestClass]
public class TrayOpenerTests
{
    [TestMethod]
    public void NotepadOpensAFileWhoseTypeHasNoCommand()
    {
        var launch = TrayOpener.Open(@"C:\notes\winland.conf", null);

        Assert.AreEqual("notepad.exe \"C:\\notes\\winland.conf\"", launch.CommandLine);
        Assert.IsNull(launch.WorkingDirectory);
    }

    [TestMethod]
    public void ABlankAssociationAlsoUsesNotepad()
    {
        Assert.AreEqual("notepad.exe \"C:\\notes\\winland.conf\"", TrayOpener.Command(@"C:\notes\winland.conf", "  "));
    }

    [TestMethod]
    public void AnUnquotedPlaceholderIsQuoted()
    {
        var command = TrayOpener.Command(
            @"C:\Program Files\winland.conf",
            @"C:\Tools\editor.exe %1");

        Assert.AreEqual("C:\\Tools\\editor.exe \"C:\\Program Files\\winland.conf\"", command);
    }

    [TestMethod]
    public void AQuotedPlaceholderKeepsThePathRaw()
    {
        var command = TrayOpener.Command(
            @"C:\Program Files\winland.conf",
            "\"C:\\Program Files\\Editor\\editor.exe\" \"%1\"");

        Assert.AreEqual(
            "\"C:\\Program Files\\Editor\\editor.exe\" \"C:\\Program Files\\winland.conf\"",
            command);
    }

    [TestMethod]
    [DataRow("%L")]
    [DataRow("%l")]
    [DataRow("%*")]
    public void TheOtherPlaceholdersAreFilled(string token)
    {
        Assert.AreEqual("editor.exe \"C:\\a.conf\"", TrayOpener.Command(@"C:\a.conf", "editor.exe " + token));
    }

    [TestMethod]
    public void ACommandWithoutAPlaceholderGainsThePath()
    {
        Assert.AreEqual("editor.exe \"C:\\a.conf\"", TrayOpener.Command(@"C:\a.conf", "editor.exe"));
    }

    [TestMethod]
    public void QuotesInThePathAreLeftOut()
    {
        Assert.AreEqual("notepad.exe \"C:\\ab.conf\"", TrayOpener.Command("C:\\a\"b.conf", null));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ABlankPathIsRejected(string path)
    {
        Assert.ThrowsExactly<ArgumentException>(() => TrayOpener.Open(path, null));
        Assert.ThrowsExactly<ArgumentException>(() => TrayOpener.Command(path, "editor.exe %1"));
    }
}
