# Cruor's buildings

The empire's capital, in the style the owner chose (2026-10-23). **Read
[docs/regions/cruor-architecture.md](../../../../../docs/regions/cruor-architecture.md) first**:
it holds the decisions (a massive cathedral, ordinary civic buildings, pale stone, red only for
the faith), the rules, and reference renders of what passed. The city's layout direction and
building budget (about 25–30 buildings) are in
[docs/regions/cruor.md](../../../../../docs/regions/cruor.md).

| File | What |
| --- | --- |
| `kit.py` | The parts: walls, city wall, buttress, pinnacle, merlons, lancet, doorway, tower, hall, gatehouse, town house; `Layered` for buildings you walk into; `piece` and `paint` |
| `cathedral.py` | The cathedral, built from the kit; run it to see it outside, inside, and inside with the near walls cut |
| `showcase.py` | Every part at ordinary size, with the cathedral behind for scale |
| `textures.py` | Cruor's painted textures, into `textures/` |

Run in Blender through the MCP:

```python
import runpy
runpy.run_path(r"C:\dev\rth\tools\art\blender\buildings\cruor\textures.py", run_name="__main__")   # after changing textures
runpy.run_path(r"C:\dev\rth\tools\art\blender\buildings\cruor\showcase.py", run_name="__main__")
```

A new building is a function of kit parts. Make it a part in `kit.py` if the city has more than
one, show it in `showcase.py`, and save a render to `docs/regions/img/` once the owner passes it.

Not yet: the buildings' insides (only the cathedral has one), dressing (banners, braziers,
furniture), and getting the painted textures into the game client.
