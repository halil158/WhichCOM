using WhichCOM.Cli;

try
{
    return ComlsApp.CreateDefault(Console.Out, Console.Error).Run(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"comls: {ex.Message}");
    return ComlsApp.ExitFailure;
}
