using LosPollosHermanos.App.Controllers;
using LosPollosHermanos.App.Views;

namespace LosPollosHermanos.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var controller = new GameController();
        Application.Run(new GameForm(controller));
    }
}
