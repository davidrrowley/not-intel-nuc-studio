# NUC LED Visualization Assets

This folder contains the base NUC device images and LED mask overlays for the LED visualization system.

## File Structure:
- `nuc-left-base.png` - Left perspective NUC device (no LEDs)
- `nuc-left-led-mask.png` - White mask for left side LEDs
- `nuc-right-base.png` - Right perspective NUC device (no LEDs) 
- `nuc-right-led-mask.png` - White mask for right side LEDs
- `nuc-skull-base.png` - Front view NUC device (no LEDs)
- `nuc-skull-led-mask.png` - White mask for skull LED
- `nuc-front-base.png` - Extended front view NUC device (no LEDs)
- `nuc-front-led-mask.png` - White mask for front bottom LED strip

## Usage:
The base images provide the realistic NUC device representation.
The LED mask images define exactly where LEDs should appear.
WinUI3 applies dynamic colors to the masks based on LED zone settings.
