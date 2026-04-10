# Week 3 Progress

## Implemented

- Difficulty progression across the full shift:
  - easy phase at start;
  - medium phase in the middle;
  - hard phase near the end.
- Customer patience and penalty pressure now scale with current difficulty.
- 30-second chef tutorial phase at shift start with guided target stations.
- NPC system:
  - chef character with contextual lines;
  - 4 customer personalities with different phrases and order preferences;
  - visible customer queue in front of the order desk.
- Station interactions are no longer one-click:
  - hold-based actions for order desk, grill, fryer, serving counter;
  - rapid-tap actions for assembly and drinks.
- World layout updated to feel like a real restaurant:
  - clear split between dining hall and kitchen;
  - stations aligned into a logical work pipeline.
- View/HUD updates for week 3 systems:
  - tutorial state;
  - difficulty display;
  - current customer and queue;
  - interaction mode/hints.
- Model tests expanded for tutorial, difficulty progression, hold/tap interactions.

## Verification

- `dotnet build .\src\LosPollosHermanos.App\LosPollosHermanos.App.csproj`
- `dotnet test .\tests\LosPollosHermanos.Tests\LosPollosHermanos.Tests.csproj`
