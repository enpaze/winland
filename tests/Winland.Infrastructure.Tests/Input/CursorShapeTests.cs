using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class CursorShapeTests
{
    [TestMethod]
    [DataRow("grabbing", (int)SYSTEM_CURSOR_ID.OCR_SIZEALL)]
    [DataRow("top_side", (int)SYSTEM_CURSOR_ID.OCR_SIZENS)]
    [DataRow("bottom_side", (int)SYSTEM_CURSOR_ID.OCR_SIZENS)]
    [DataRow("left_side", (int)SYSTEM_CURSOR_ID.OCR_SIZEWE)]
    [DataRow("right_side", (int)SYSTEM_CURSOR_ID.OCR_SIZEWE)]
    [DataRow("top_left_corner", (int)SYSTEM_CURSOR_ID.OCR_SIZENWSE)]
    [DataRow("bottom_right_corner", (int)SYSTEM_CURSOR_ID.OCR_SIZENWSE)]
    [DataRow("top_right_corner", (int)SYSTEM_CURSOR_ID.OCR_SIZENESW)]
    [DataRow("bottom_left_corner", (int)SYSTEM_CURSOR_ID.OCR_SIZENESW)]
    public void EachNameTheCoreReportsHasAWindowsCursor(string name, int cursor) =>
        Assert.AreEqual((SYSTEM_CURSOR_ID)cursor, CursorShape.For(name));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("pirate")]
    public void NoNameOrAnUnknownOneIsTheUsersOwnCursor(string? name) =>
        Assert.IsNull(CursorShape.For(name));
}
