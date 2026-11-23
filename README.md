# LightningChart MAUI Example

This .NET MAUI example displays patient vital signs from `Data/patient_10.csv` in stacked chart sections. Systolic and diastolic share a blood-pressure axis. The recording can be viewed as historical data or replayed using its timestamps.

Learn more: [LightningChart documentation](https://lightningchart.com/lc-la/docs/)

![MAUI example](./images/MauiVitalSigns.png)

## Prerequisites

- Windows 10 build 19041 or later
- .NET 10 SDK with the MAUI workload
- LightningChart JS license key ([get one here](https://lightningchart.com/js-charts/))

Install the MAUI workload if needed:
```powershell
dotnet workload install maui
```

## Build and Run

### Standalone repository

1. Clone the standalone example with:
   ```powershell
   git clone https://github.com/Lightning-Chart/lc-la-example-maui.git
   cd lc-la-example-maui
   ```

2. Set your license key:
   ```powershell
   $env:LCJS_LICENSE_KEY="your-license-key"
   ```

3. Run the Windows example from that same PowerShell session:
   ```powershell
   dotnet build .\MauiVitalSigns.csproj -t:Run -f net10.0-windows10.0.19041.0
   ```

   You can also open `MauiVitalSigns.csproj` in Visual Studio, select the Windows target, and run the project from the same environment.

### From the lc-la repository

1. Clone the repository:
   ```powershell
   git clone https://github.com/Lightning-Chart/lc-la.git
   cd lc-la
   ```

   If you already have the repository, open a terminal in its root folder.

2. Install dependencies:
   ```powershell
   npm install
   ```

3. Copy `.env.example` to `.env` in the repository root and set `LCJS_LICENSE_KEY` to your license key.

4. Start the Windows example:
   ```powershell
   npm run dev
   ```

   Select ".NET MAUI", then "Patient vital signs".

## Using the Example

The app initially loads the historical recording. Select **Start replay** to replay the measurements.
Select **Pause replay**, then **Resume replay**, to pause and continue. 
Select **Load historical** to stop replay and display the historical recording.