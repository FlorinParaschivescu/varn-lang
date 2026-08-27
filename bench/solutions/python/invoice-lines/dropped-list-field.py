import json
import sys


def solve(cart):
    subtotal = 0
    units = 0
    for line in cart["lines"]:
        if line["qty"] < 1:
            return {"ok": False, "error": "invalid quantity for sku: " + line["sku"]}
        subtotal += line["qty"] * line["unitCents"]
        units += line["qty"]
    return {"ok": True, "value": {"subtotalCents": subtotal, "unitCount": units}}


print(json.dumps(solve(json.load(sys.stdin))))
