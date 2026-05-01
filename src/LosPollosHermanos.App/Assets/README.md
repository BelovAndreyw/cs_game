# Assets

Week 4 introduces a drop-in asset pipeline for the WinForms client.

If a matching PNG exists in the published `Assets/` folder, the renderer uses it.
If no file exists, the game falls back to the built-in pixel-art sprites generated in code.

Supported folders:

- `Assets/player/`
- `Assets/npcs/`
- `Assets/stations/`
- `Assets/tiles/`

Examples:

- `Assets/player/idle-down-0.png`
- `Assets/player/walk-left-1.png`
- `Assets/npcs/chef.png`
- `Assets/npcs/customer-0.png`
- `Assets/stations/orderdesk.png`
- `Assets/tiles/floor-kitchen-0.png`
