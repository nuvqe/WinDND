namespace WinDND.Infrastructure;

internal interface IAppLogger
{
    void Information(string message, int eventId = 1000);

    void Error(string message, Exception? exception = null, int eventId = 2000);
}
