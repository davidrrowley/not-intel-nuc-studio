# Intel NUC LED Controller

Clean, GUI-ready class library for controlling Intel NUC LED lighting.

## Project Structure

- **NucLedController.Core** - Main class library (reference this in your GUI)
- **NucLedController.Console** - Simple console test application

## Core Features

### Working Command Sequences
Based on proven Intel ENE protocol implementation:
- **Pattern → Rainbow Disable → Color → Brightness** sequence
- Zone mapping: A=Skull, B=BottomLeft, C=BottomRight, D=FrontBottom  
- Color channels: C1, C2, C3, C4

### Key Classes

#### `NucLedController`
Main controller class with async methods:
- `ConnectAsync(portName)` - Connect to COM port
- `SetZoneColorAsync(zone, color)` - Set individual zone color
- `SetAllZonesAsync(color)` - Set all zones to same color
- `DisableRainbowAsync()` - Aggressive rainbow mode killer
- `TurnOnAsync()` / `TurnOffAsync()` - LED power control

#### `LedZone` Enum
- `Skull` - Zone A (logo header)
- `BottomLeft` - Zone B  
- `BottomRight` - Zone C
- `FrontBottom` - Zone D

#### `LedColors` Constants
- Red=0, Green=96, Blue=160, Yellow=70, Purple=200, Cyan=128, White=255

## For GUI Development

### Events for Data Binding
```csharp
controller.ConnectionChanged += (s, connected) => { /* Update UI */ };
controller.StatusChanged += (s, status) => { /* Update status bar */ };
```

### Async Methods for Responsiveness
All operations are async to prevent UI freezing.

### Error Handling
All methods return `LedCommandResult` with success/failure information.

## Testing

Run the console app to test functionality:
```bash
cd NucLedController.Console
dotnet run
```

## Integration

To use in your GUI project:
1. Reference `NucLedController.Core.csproj`
2. Create instance: `var controller = new NucLedController();`
3. Connect: `await controller.ConnectAsync("COM3");`
4. Control LEDs: `await controller.SetZoneColorAsync(LedZone.Skull, LedColors.Red);`

## Proven Working Sequences

This library preserves the exact command sequences that work:
- **Rainbow Prevention**: Pattern first, then AR:0 commands
- **Zone Control**: Individual P1 → R:0 → C1:color → V:brightness
- **Initialization**: Triple attempt with aggressive rainbow killing

