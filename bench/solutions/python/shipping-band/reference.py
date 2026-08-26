import json
import sys


def band(grams):
    if grams <= 1000:
        return 500
    if grams <= 5000:
        return 900
    return 1500


def solve(parcel):
    if parcel["grams"] < 0:
        return {"ok": False, "error": "negative weight"}
    cost = band(parcel["grams"])
    if parcel["express"]:
        cost = cost * 2
    return {"ok": True, "value": {"cost": cost}}


print(json.dumps(solve(json.load(sys.stdin))))
