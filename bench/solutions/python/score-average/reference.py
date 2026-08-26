import json
import sys


def truncating_div(numerator, denominator):
    quotient = abs(numerator) // abs(denominator)
    return -quotient if (numerator < 0) != (denominator < 0) else quotient


def solve(scores):
    values = scores["values"]
    count = len(values)
    if count == 0:
        return {"ok": False, "error": "no scores"}
    return {"ok": True, "value": {"mean": truncating_div(sum(values), count), "count": count}}


print(json.dumps(solve(json.load(sys.stdin))))
