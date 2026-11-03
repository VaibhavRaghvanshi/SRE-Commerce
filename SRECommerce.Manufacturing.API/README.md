# Manufacturing API

Owns `SRECommerce_ManufacturingDB`. Enable the Manufacturing tenant module to use `/workspace/:tenant/manufacturing`. A BOM captures finished SKU, material quantities and standard costs, and ordered stages. Saving a code again creates a new revision. Existing production orders keep their original BOM quantities and cost estimate.

Starting an order uses Inventory's own reservation and commit APIs. A worker records each transition. After inputs are consumed, the order can record stages, quality checks, wastage and output. Positive output requires a passing quality check. The worker receives a single finished lot using the production ID as the Inventory receipt key. Retrying after a dropped response reuses the same receipt.

An order can cancel before consumption. A full-scrap run records the consumed materials and produces no stock. The standard cost is an estimate, and overhead is recorded separately. Runtime Inventory calls require the scoped Manufacturing client. The default local launcher does not provision it.
