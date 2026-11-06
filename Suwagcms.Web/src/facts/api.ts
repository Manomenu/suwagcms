// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type Fact = components["schemas"]["Fact"];
export type SaveFact = components["schemas"]["SaveFact"];

export const factsApi = resourceApi<Fact, SaveFact>("facts");
