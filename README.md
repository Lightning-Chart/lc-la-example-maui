# LightningChart for MAUI

This .NET MAUI example displays patient vital signs from `examples/data/patient_10.csv`. The recording contains 1,800 samples and can be viewed as historical data or replayed using its timestamps.

Learn more: [LightningChart documentation](https://lightningchart.com/lc-la/docs/)

![MAUI example](/examples/maui/lcla_maui.png)

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
   dotnet build .\LightningChartMauiExample.csproj -t:Run -f net10.0-windows10.0.19041.0
   ```

   You can also open `LightningChartMauiExample.csproj` in Visual Studio, select a target device or platform, and run the project from the same environment.

4. The app initially displays the complete historical recording. Select **Start replay** to replay the measurements. Select **Pause replay**, then **Resume replay**, to pause and continue. Select **Load historical** to stop replay and display the complete recording.