Certain areas are visited quite frequently, and especially if you enter/leave buildings it often makes sense to navigate
via a waypoint on the outside (with `"Mount": true` if coming from the inside, and `"Fly": true` if coming from the
outside).

This vastly improves the pathfinding performance, and avoids attempting to fly e.g. under the map or into the building
that can sometimes be found as valid paths.

## Mor Dhona

```json
        {
          "Position": {
            "X": 30.917934,
            "Y": 20.495003,
            "Z": -656.1909
          },
          "TerritoryId": 156,
          "InteractionType": "WalkTo",
          "Fly": true,
          "$": "Rising Stones Door"
        }
```

## Coerthas Central Highlands

```json
        {
          "Position": {
            "X": 240.2761,
            "Y": 302.6276,
            "Z": -199.78418
          },
          "TerritoryId": 155,
          "InteractionType": "WalkTo",
          "$": "Camp Dragonhead, door to Haurchefant",
          "Fly": true,
          "AetheryteShortcut": "Coerthas Central Highlands - Camp Dragonhead"
        }
```
