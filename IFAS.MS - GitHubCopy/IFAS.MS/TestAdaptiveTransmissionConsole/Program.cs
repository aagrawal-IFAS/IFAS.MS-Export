// See https://aka.ms/new-console-template for more information
using IFAS.MS.AdaptiveTransmissionAnalyzer;

Console.WriteLine("Hello, World!");
AdaptiveTransmissionAnalyzer.Analyze().GetAwaiter();

while (true)
{
    Console.WriteLine($"Speed {AdaptiveTransmissionAnalyzer.CurrentSpeedInMbps} Mbps");
    Console.WriteLine($"CurrentObjectCount {AdaptiveTransmissionAnalyzer.CurrentObjectCount}");
    Task.Delay(1000).GetAwaiter().GetResult();
}
