import json
import sys


def solve(cart):
    subtotal = 0
    units = 0
    free = []
    for line in cart["lines"]:
        if line["qty"] < 1:
            return {"ok": False, "error": "invalid quantity for sku: " + line["sku"]}
        subtotal += line["qty"] * line["unitPrice"]
        units += line["qty"]
        if line["unitPrice"] == 0:
            free.append(line["sku"])
    return {"ok": True, "value": {"subtotalCents": subtotal, "unitCount": units, "freeSkus": free}}


print(json.dumps(solve(json.load(sys.stdin))))
