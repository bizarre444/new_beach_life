# BeachLife

A beach-resort management game in the spirit of *Beach Life* (2002): lay out a plot of beach,
build bars, restaurants, toilets, pools and hotel blocks, set prices, and keep guests happy
enough that the resort's reputation — and therefore its guest count — keeps growing.

This repository contains the playable base: the full simulation plus a code-only Unity front end
with placeholder art.

## Layout

```
unity/BeachLife/          Unity project (2022.3 LTS)
  Assets/Scripts/Core/    Simulation. Plain C#, no UnityEngine references.
  Assets/Scripts/Unity/   Rendering, camera, build tool, HUD.
  Assets/Editor/          "BeachLife/Create Resort Scene" menu item.
tools/CoreTests/          xUnit tests over the same Core sources (dotnet test).
tools/SimRunner/          Headless balance harness that prints a per-day ledger.
tools/UnityCompileCheck/  Compiles the runtime scripts against Unity reference assemblies.
reference/                Drop the original game here for balance comparison (git-ignored).
```

The simulation is deliberately engine-agnostic: `Resort` owns the grid, buildings, guests, money
and clock, and the Unity layer only renders its state and forwards input. That is what makes the
headless tests and the balance harness possible.

## Running it

**In Unity**: open `unity/BeachLife` with Unity 2022.3 LTS and press Play — an empty scene
bootstraps itself. For a saved scene, use the menu item *BeachLife → Create Resort Scene*.

**Headless**, without the editor:

```bash
dotnet test  tools/CoreTests          # simulation tests
dotnet run --project tools/SimRunner -- 5   # simulate 5 days and print the ledger
dotnet build tools/UnityCompileCheck  # type-check the Unity scripts
```

## Controls

| Input | Action |
| --- | --- |
| WASD / arrows / middle-drag | Pan |
| Q / E | Rotate |
| Mouse wheel | Zoom |
| Left click | Place, demolish or inspect, depending on the mode |
| X | Demolish mode |
| Esc | Back to select mode |

The top bar holds cash, the clock, guest count, reputation and the speed controls (pause, 1x, 3x, 8x).

## What the simulation does

- **Grid plot** with water, sand, promenade and grass; sun loungers and parasols only go on sand.
- **Buildings** have a footprint, build cost, daily upkeep, capacity, service time, a price the
  player can scale between 0.5x and 2x, and a set of needs they satisfy.
- **Guests** arrive through the entrance, decay seven needs (hunger, thirst, bladder, hygiene,
  fun, energy, tan), pick the best venue for their most urgent need (weighted by distance, price,
  dirtiness and queue), queue, pay, get served, and leave in a mood that feeds resort reputation.
- **Reputation** drives how many guests arrive the next day; angry walkouts drag it down.
- **Economy** tracks per-day income, upkeep and construction spend, and closes the books at 02:00.
- Placing a building that would wall guests off is rejected — a flood fill from the entrance runs
  before every placement is committed.

## Not built yet

Staff (cleaners, lifeguards, bar staff), litter and cleanliness as a visible system, weather and
seasons, marketing campaigns, guest groups and personalities, hotel check-in as a separate stay
loop, save/load, and real art. The placeholder primitives are there so the systems can be played
and tuned before any of that is modelled.
