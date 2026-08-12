# LightningChart for MAUI

This .NET MAUI example loads 1,000,000 historical samples and then streams 10,000-sample batches.

## Run

1. Install the .NET 8 SDK or later and the matching .NET MAUI workload.
2. Set a LightningChart JS license key:

   ```powershell
   $env:LCJS_LICENSE_KEY="your-license-key"
   ```

3. Run the Windows target:

   ```bash
   dotnet build -t:Run -f net8.0-windows10.0.19041.0
   ```

Select an Android, iOS, or Mac Catalyst target in your IDE to run the same project there.
