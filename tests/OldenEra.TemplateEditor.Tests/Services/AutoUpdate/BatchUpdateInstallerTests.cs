using OldenEra.TemplateEditor.Services.AutoUpdate;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

public class BatchUpdateInstallerTests
{
    [Fact]
    public void BuildInstallScript_containsMoveStartDel()
    {
        string script = BatchUpdateInstaller.BuildInstallScript(
            @"C:\Temp\new.exe",
            @"C:\Program Files\OldenEra\OldenEraTemplateGenerator.exe");

        Assert.Contains("timeout /t 2 /nobreak", script);
        Assert.Contains("move /y \"C:\\Temp\\new.exe\" \"C:\\Program Files\\OldenEra\\OldenEraTemplateGenerator.exe\"", script);
        Assert.Contains("start \"\" \"C:\\Program Files\\OldenEra\\OldenEraTemplateGenerator.exe\"", script);
        Assert.Contains("del \"%~f0\"", script);
    }

    [Fact]
    public void BuildInstallScript_quotesPathsWithSpaces()
    {
        string script = BatchUpdateInstaller.BuildInstallScript(
            @"C:\Users\Foo Bar\new.exe",
            @"C:\Program Files\App\app.exe");

        Assert.Contains("\"C:\\Users\\Foo Bar\\new.exe\"", script);
        Assert.Contains("\"C:\\Program Files\\App\\app.exe\"", script);
    }
}
