# BeachLife

A beach-resort management game in the spirit of *Beach Life* (2002): lay out a plot of beach,
build bars, restaurants, toilets, pools and hotel blocks, set prices, hire staff and run
marketing, and keep guests happy enough that the resort's reputation — and therefore its guest
count — keeps growing.

The whole game ships as one self-contained Windows build:

```
build/Windows/BeachLife.exe
```

…plus the full Unity project and headless tooling for tests and balance work.

## Layout

```
unity/BeachLife/          Unity project (Unity 6 / 6000.4.8f1)
  Assets/Scripts/Core/    Simulation. Plain C#, no UnityEngine references.
  Assets/Scripts/Unity/   Rendering, camera, build tool, HUD, weather FX, save slots.
  Assets/Editor/          Scene creation, batch smoke test, Windows build script.
tools/CoreTests/          xUnit tests over the same Core sources (dotnet test).
tools/SimRunner/          Headless balance harness that prints a per-day ledger.
tools/UnityCompileCheck/  Compiles the runtime scripts against Unity reference assemblies.
build/Windows/            Built game output (git-ignored).
reference/                Drop the original game here for balance comparison (git-ignored).
```

The simulation is deliberately engine-agnostic: `Resort` owns the grid, buildings, guests, staff,
money, weather and clock, and the Unity layer only renders its state and forwards input. That is
what makes the headless tests, the balance harness and the save-system snapshots possible.

## Running it

**Standalone exe** — rebuild it any time from the shell (needs Unity 6 installed):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" \
  -batchmode -projectPath unity/BeachLife \
  -executeMethod BeachLife.EditorTools.BuildWindowsPlayer.Run \
  -logFile build/unity_build.log
```

**In Unity**: open `unity/BeachLife` with Unity 6 and press Play — an empty scene bootstraps
itself. For a saved scene, use the menu item *BeachLife → Create Resort Scene*.

**Headless**, without the editor:

```bash
dotnet test  tools/CoreTests          # simulation tests (37 and counting)
dotnet run --project tools/SimRunner -- 7   # simulate 7 days and print the ledger
dotnet build tools/UnityCompileCheck  # type-check the Unity scripts
```

**Batch smoke test** — enters play mode, builds a resort, simulates three days, round-trips a
save through the real JSON serializer, writes `build/beachlife_report.json` and a screenshot:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" \
  -batchmode -projectPath unity/BeachLife \
  -executeMethod BeachLife.EditorTools.BatchSmokeTest.Run \
  -logFile build/unity_smoke.log
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

The top bar holds cash, the clock and today's weather (plus tomorrow's forecast), guest count,
reputation, walkouts, the speed controls (pause, 1x, 3x, 8x) and the Staff / Ads / Goals / Save
panels. Click a building to inspect it: price slider, attendant assignment, demolish.

## What the simulation does

- **Grid plot** with water, sand, promenade and grass; loungers, parasols, the volleyball court
  and the lifeguard tower only go on sand.
- **Buildings** (17 kinds) have a footprint, build cost, daily upkeep, capacity, service time, a
  price the player can scale between 0.5x and 2x, and a set of needs they satisfy.
- **Guests** arrive through the entrance with one of three spending personalities (budget,
  regular, VIP) that set their budget, price tolerance and messiness. They decay seven needs
  (hunger, thirst, bladder, hygiene, fun, energy, tan), pick the best venue for their most urgent
  need (weighted by distance, price, dirtiness, litter and queue), queue, pay, get served, and
  leave in a mood that feeds resort reputation.
- **Hotels**: some guests book a multi-day stay on arrival, holding a bed. At night they walk to
  their hotel, pay the nightly rate and sleep until 07:30, then spend the day in the resort until
  their stay is over.
- **Weather** rolls each morning (sunny / heatwave / cloudy / rain) and is forecast in the top
  bar. Heat drives thirst, rain kills tanning and arrivals; rain also washes litter away.
- **Litter** piles up where guests wander, is reduced by nearby bins, and drags down mood and
  venue attractiveness. **Cleaners** clear it and scrub filthy venues.
- **Staff**: cleaners work on their own; attendants are assigned to a venue and add capacity and
  service speed. Everyone earns a daily wage.
- **Marketing** campaigns (flyers → regional → TV) boost arrivals for a daily fee.
- **Goals** pay cash rewards (guest counts, reputation, profit, first hotel night, cash total).
- **Reputation** drives how many guests arrive the next day; angry walkouts drag it down.
- **Economy** tracks per-day income, upkeep, wages, marketing and construction spend, and closes
  the books at 02:00. Three consecutive days in the red means **bankruptcy** and a game-over
  screen with a restart button.
- Placing a building that would wall guests off is rejected — a flood fill from the entrance runs
  before every placement is committed.
- **Save/load**: three slots in `Application.persistentDataPath`, full-state JSON snapshots
  (terrain, buildings, guests, staff, litter, weather, goals, ledger, clock).

## Not built yet

Guest groups travelling together, seasons, more event variety, and real art — the code-only
placeholder primitives are deliberately readable so the systems can still be tuned.
