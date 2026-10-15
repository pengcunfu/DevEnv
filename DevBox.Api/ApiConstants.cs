namespace DevBox.Api;

public static class ApiConstants
{
    public const int UiPort = 35173;
    public const int ApiPort = 35174;
    public const string Loopback = "127.0.0.1";

    public static string UiBaseUrl => $"http://{Loopback}:{UiPort}";
    public static string ApiBaseUrl => $"http://{Loopback}:{ApiPort}";
}
