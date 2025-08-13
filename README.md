# Intel NUC Software Studio Clone

A WinUI 3 application that replicates the Intel NUC Software Studio interface with modern circular gauges and responsive design.

## Features

- **System Monitor Dashboard**: Real-time circular gauges displaying CPU, Memory, and Storage metrics
- **Performance Page**: Advanced performance monitoring with animated gauges
- **LED Controller**: Interface for controlling NUC LED lighting
- **Responsive Design**: Adaptive layout that works across different window sizes

## Technology Stack

- **Framework**: .NET 9.0
- **UI**: WinUI 3
- **Target Platform**: Windows 10/11
- **Architecture**: x86, x64, ARM64

## Project Structure

```
NucLedController/
├── NucLedController.Core/          # Core business logic and models
├── NucLedController.Console/       # Console application for testing
└── NucLedController.WinUI3/        # Main WinUI 3 application
    ├── Views/                      # Application pages
    ├── Controls/                   # Custom UI controls (CircularGauge)
    └── Assets/                     # Application assets
```

## Getting Started

### Prerequisites

- Windows 10 version 1903 or later
- .NET 9.0 SDK
- Visual Studio 2022 (recommended) or Visual Studio Code

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
dotnet run --project NucLedController/NucLedController.WinUI3
```

## Custom Controls

### CircularGauge

A custom WinUI 3 control that displays circular progress indicators with:
- Animated progress arcs
- Customizable colors and sizes
- Center value and unit display
- Responsive design

## Contributing

Feel free to submit issues and pull requests. This project is a learning exercise in WinUI 3 development and modern Windows app design.

## License

This project is for educational purposes and is not affiliated with Intel Corporation.
