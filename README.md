# LightningChart for MAUI

This .NET MAUI 10 example opens in historical mode with 1,000,000 samples. Use the real-time control to start or stop 10,000-sample batches.

Learn more: [LightningChart documentation](https://lightningchart.com/lc-la/docs/)

## Run

1. Install the .NET 10 SDK and its MAUI workload:

   ```powershell
   dotnet workload install maui
   ```
2. Set a LightningChart JS license key (download free key from [lightningchart.com](https://lightningchart.com/js-charts/)):

   ```powershell
   $env:LCJS_LICENSE_KEY="your-license-key"
   ```

3. Run the Windows example from that same PowerShell session:

   ```powershell
   dotnet build .\LightningChartMauiExample.csproj -t:Run -f net10.0-windows10.0.19041.0 -p:LclaUseLocalSource=true
   ```

   You can also open `LightningChartMauiExample.csproj` in Visual Studio, select a target device or platform, and run the project from the same environment.
