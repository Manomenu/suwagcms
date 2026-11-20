import { expect, type APIRequestContext } from "@playwright/test";

export function freshId(prefix: string): string {
    return `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export function testBots(prefix: string) {
    return {
        freshId: () => freshId(prefix),
        newBot: async (request: APIRequestContext): Promise<string> => {
            const id = freshId(prefix);
            const response = await request.post("/api/bots", {
                data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` },
            });
            expect(response.status()).toBe(201);
            return id;
        },
    };
}
