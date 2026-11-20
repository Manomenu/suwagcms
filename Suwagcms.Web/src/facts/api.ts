import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type Fact = components["schemas"]["Fact"];
export type SaveFact = components["schemas"]["SaveFact"];

export const factsApi = resourceApi<Fact, SaveFact>("facts");
