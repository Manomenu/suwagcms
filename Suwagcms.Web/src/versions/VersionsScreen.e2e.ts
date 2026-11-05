// A person creates a version of a bot in the browser; the bot is in the address, and a refusal lists every mistake.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

function freshId(): string {
    return `versions-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function addConversation(request: APIRequestContext, bot: string): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/procedures/conversation?ifRevision=0`, {
        data: {
            description: "The base of every conversation",
            firstStep: "wait",
            steps: { wait: { listen: {}, next: "answer" }, answer: { say: "Hello", next: "wait" } },
        },
    });
    expect(response.status()).toBe(200);
}

test("AC-version-create-2: the web lists every mistake of an unfinished bot, links to the screen to mend it, and creates no version", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/versions`);
    await expect(page.getByText("No versions yet")).toBeVisible();

    await page.getByRole("button", { name: "Create version" }).click();

    await expect(page.getByText("Not created, 1 mistake")).toBeVisible();
    await expect(page.getByText(/no 'conversation' procedure/)).toBeVisible();
    await expect(page.getByRole("link", { name: "Open", exact: true })).toHaveAttribute("href", `/bots/${bot}/procedures`);
    await expect(page.getByText("No versions yet")).toBeVisible();
});

test("AC-version-create-1: Create version in the web makes the next version and shows it in the list, which can be opened (AC-version-create-13)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addConversation(request, bot);
    await page.goto(`/bots/${bot}/versions`);

    await page.getByRole("button", { name: "Create version" }).click();

    const row = page.getByRole("row").filter({ hasText: "v1 ·" });
    await expect(row).toContainText("0 facts, 0 knowledge bases, 0 articles, 1 procedures");
    await row.getByRole("button", { name: "Open" }).click();
    await expect(page.getByText(`"bot":"${bot}"`)).toBeVisible();
    const listed = await request.get(`/api/bots/${bot}/versions`);
    expect(await listed.json()).toMatchObject([{ number: 1, procedures: 1 }]);

    await page.getByRole("button", { name: "Create version" }).click();
    await expect(page.getByText(/Nothing has changed since version 1/)).toBeVisible();
});

/** A bot that keeps the contract of the SDK's two routes and nothing else, so the page can be pushed to without a .NET bot beside the browser. */
async function startBot(id: string): Promise<{ address: string; server: Server; has: () => number | null }> {
    let current: number | null = null;
    const server = createServer((request, response) => {
        const put = /^\/suwagcms\/versions\/(\d+)$/.exec(request.url ?? "");
        const state = () => JSON.stringify({ bot: id, current, held: current === null ? [] : [current], inUse: [], storeError: null });
        if (request.method === "PUT" && put) {
            request.resume().on("end", () => {
                current = Number(put[1]);
                response.writeHead(200, { "content-type": "application/json" }).end(state());
            });
        } else {
            response.writeHead(200, { "content-type": "application/json" }).end(state());
        }
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    return { address: `http://127.0.0.1:${(server.address() as AddressInfo).port}`, server, has: () => current };
}

test("AC-version-push-1: Push in the web makes the bot hold the latest version, and the page then shows the number the bot reports", async ({
    page,
    request,
}) => {
    const id = freshId();
    const bot = await startBot(id);
    try {
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: bot.address } })).status()).toBe(201);
        await addConversation(request, id);
        expect((await request.post(`/api/bots/${id}/versions`)).status()).toBe(201);
        await page.goto(`/bots/${id}/versions`);
        await expect(page.getByText("Bot has none · Latest v1")).toBeVisible();

        await page.getByRole("button", { name: "Push v1" }).click();

        await expect(page.getByText("Bot has v1 · up to date")).toBeVisible();
        expect(bot.has()).toBe(1);
        await expect(page.getByRole("button", { name: "Push v1" })).toHaveCount(0);
    } finally {
        bot.server.close();
    }
});

test("AC-version-diff-10: Changes on a version compares it with the one before, a select compares it with another, and the first offers neither (AC-version-diff-1)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addConversation(request, bot);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    const fact = { type: "text", description: "The city", maxLength: 80 };
    expect((await request.put(`/api/bots/${bot}/facts/customer.city?ifRevision=0`, { data: fact })).status()).toBe(200);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    expect((await request.put(`/api/bots/${bot}/facts/customer.city`, { data: { ...fact, maxLength: 120 } })).status()).toBe(200);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    await page.goto(`/bots/${bot}/versions`);

    await expect(page.getByRole("row").filter({ hasText: "v1 ·" }).getByRole("button", { name: "Changes" })).toHaveCount(0);
    await expect(page.getByRole("row").filter({ hasText: "v1 ·" })).toContainText("Nothing before it");

    await page.getByRole("row").filter({ hasText: "v3 ·" }).getByRole("button", { name: "Changes" }).click();
    await expect(page.getByText("0 added · 0 removed · 1 changed")).toBeVisible();
    await expect(page.getByText("maxLength:")).toBeVisible();

    await page.getByLabel("From").selectOption("1");
    await expect(page.getByText("1 added · 0 removed · 0 changed")).toBeVisible();
});

test("AC-version-diff-11: the address --open prints shows the same comparison, and AC-version-diff-13: the page compares what the bot has with the latest", async ({
    page,
    request,
}) => {
    const id = freshId();
    const bot = await startBot(id);
    try {
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: bot.address } })).status()).toBe(201);
        await addConversation(request, id);
        expect((await request.post(`/api/bots/${id}/versions`)).status()).toBe(201);
        expect((await request.post(`/api/bots/${id}/versions/1/push`)).status()).toBe(200);
        const fact = { type: "text", description: "The city", maxLength: 80 };
        expect((await request.put(`/api/bots/${id}/facts/customer.city?ifRevision=0`, { data: fact })).status()).toBe(200);
        expect((await request.post(`/api/bots/${id}/versions`)).status()).toBe(201);

        await page.goto(`/bots/${id}/versions?from=2&to=1`);
        await expect(page.getByText("0 added · 1 removed · 0 changed")).toBeVisible();
        await expect(page).toHaveURL(`/bots/${id}/versions`);

        await page.getByRole("button", { name: "Compare v1 with v2" }).click();
        await expect(page.getByText("1 added · 0 removed · 0 changed")).toBeVisible();
    } finally {
        bot.server.close();
    }
});

test("AC-version-export-10: the address export --open prints opens that version", async ({ page, request }) => {
    const bot = await newBot(request);
    await addConversation(request, bot);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);

    await page.goto(`/bots/${bot}/versions?open=1`);

    await expect(page.getByRole("heading", { name: "Version 1" })).toBeVisible();
    await expect(page.getByText(`"bot":"${bot}"`)).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/versions`);
});
