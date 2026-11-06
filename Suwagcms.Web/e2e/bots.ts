// Why: a run shares one database, so every test makes its own bot, and a name saying which file made it helps read what is left behind.
import { expect, type APIRequestContext } from "@playwright/test";

export function freshId(prefix: string): string {
    return `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

/** The two every browser test starts from, for one file's prefix. */
export function testBots(prefix: string) {
    return {
        freshId: () => freshId(prefix),
        newBot: async (request: APIRequestContext): Promise<string> => {
            const id = freshId(prefix);
            const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
            expect(response.status()).toBe(201);
            return id;
        },
    };
}
