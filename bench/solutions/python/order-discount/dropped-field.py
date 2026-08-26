import json
import sys

RATES = {"gold": 10, "silver": 5, "basic": 0}


def solve(order):
    total = sum(order["items"])
    tier = order["customerTier"]
    if tier not in RATES:
        return {"ok": False, "error": "unknown tier: " + tier}
    return {"ok": True, "value": {"total": total}}


print(json.dumps(solve(json.load(sys.stdin))))
