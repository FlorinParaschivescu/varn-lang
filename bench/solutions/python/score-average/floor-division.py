import json
import sys


def solve(scores):
    values = scores["values"]
    count = len(values)
    if count == 0:
        return {"ok": False, "error": "no scores"}
    return {"ok": True, "value": {"mean": sum(values) // count, "count": count}}


print(json.dumps(solve(json.load(sys.stdin))))
