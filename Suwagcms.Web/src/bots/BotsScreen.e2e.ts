import { expect, test, type APIRequestContext } from "@playwright/test";
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";
import { freshId } from "../../e2e/bots";

async function register(request: APIRequestContext, id: string): Promise<void> {
    const response = await request.post("/api/bots", {
        data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` },
    });
    expect(response.status()).toBe(201);
}

test("AC-bot-profiles-2: registering a bot opens its profile at /bots/<id>, and AC-bot-profiles-1: it is in the list", async ({ page }) => {
    const id = freshId("web");
    await page.goto("/bots");

    await page.getByRole("button", { name: "Register a bot" }).click();
    await page.getByLabel("Id").fill(id);
    await page.getByLabel("Name", { exact: true }).fill("Registered in the web");
    await page.getByLabel("Address").fill("http://registered.internal:8080");
    await page.getByRole("button", { name: "Register", exact: true }).click();

    await expect(page).toHaveURL(`/bots/${id}`);
    await expect(page.getByRole("navigation").getByRole("listitem").filter({ hasText: "Registered in the web" })).toHaveAttribute(
        "aria-current",
        "page",
    );
    await page.goto("/bots");
    await expect(page.getByRole("main").getByRole("link", { name: "Registered in the web" })).toBeVisible();
    await expect(page.getByRole("main").getByText("http://registered.internal:8080")).toBeVisible();
});

test("AC-bot-profiles-4: a refused registration shows every mistake with its fix and registers nothing", async ({ page }) => {
    await page.goto("/bots");
    await page.getByRole("button", { name: "Register a bot" }).click();
    await page.getByLabel("Address").fill("not an address");

    await page.getByRole("button", { name: "Register", exact: true }).click();

    const alert = page.getByRole("alert");
    await expect(alert).toContainText("The id is missing or not valid");
    await expect(alert).toContainText("The name is blank.");
    await expect(alert).toContainText("Send the address where the CMS reaches the bot");
    await expect(page).toHaveURL("/bots");
});

test("AC-bot-profiles-8: switching the profile in one tab changes neither another tab's bot nor anything on the server", async ({
    context,
    request,
}) => {
    const first = freshId("tab-a");
    const second = freshId("tab-b");
    await register(request, first);
    await register(request, second);
    const tabOne = await context.newPage();
    const tabTwo = await context.newPage();
    await tabOne.goto(`/bots/${first}`);
    await tabTwo.goto(`/bots/${first}`);
    const changes: string[] = [];
    tabOne.on("request", (call) => {
        if (call.url().includes("/api/") && call.method() !== "GET") changes.push(`${call.method()} ${call.url()}`);
    });

    await tabOne
        .getByRole("navigation")
        .getByRole("link", { name: `Name of ${second}` })
        .click();

    await expect(tabOne).toHaveURL(`/bots/${second}`);
    await tabTwo.reload();
    await expect(tabTwo).toHaveURL(`/bots/${first}`);
    await expect(
        tabTwo
            .getByRole("navigation")
            .getByRole("listitem")
            .filter({ hasText: `Name of ${first}` }),
    ).toHaveAttribute("aria-current", "page");
    expect(changes).toEqual([]);
});

async function startDeclaringBot(id: string, blocks: () => string[]): Promise<{ address: string; server: Server }> {
    const server = createServer((_, response) => {
        response.writeHead(200, { "content-type": "application/json" }).end(
            JSON.stringify({
                bot: id,
                formatVersion: 5,
                blocks: blocks(),
                codeActions: [{ id: "lookupPolicy", description: "Finds a policy" }],
            }),
        );
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    return { address: `http://127.0.0.1:${(server.address() as AddressInfo).port}`, server };
}

test("AC-bot-declaration-4: opening the profile asks the bot and shows its answer, and Ask again shows the new one (AC-bot-declaration-9)", async ({
    page,
    request,
}) => {
    const id = freshId("declaring");
    let blocks = ["listen", "say"];
    const bot = await startDeclaringBot(id, () => blocks);
    try {
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: bot.address } })).status()).toBe(201);

        await page.goto(`/bots/${id}`);

        const panel = page.getByRole("main");
        await expect(panel.getByText("Asked just now")).toBeVisible();
        await expect(panel.getByText("say", { exact: true })).toBeVisible();
        await expect(panel.getByText("lookupPolicy")).toBeVisible();

        blocks = ["listen"];
        await panel.getByRole("button", { name: "Ask again" }).click();
        await expect(panel.getByText("say", { exact: true })).toHaveCount(0);
        await expect(panel.getByText("listen", { exact: true })).toBeVisible();
    } finally {
        bot.server.close();
    }
});

test("AC-bot-declaration-5: a bot that has never answered shows that it has not, and the rest of the profile works", async ({
    page,
    request,
}) => {
    const id = freshId("silent");
    expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: "http://127.0.0.1:9" } })).status()).toBe(201);

    await page.goto(`/bots/${id}`);

    await expect(page.getByText(/it has never answered/)).toBeVisible();
    await expect(page.getByRole("main").getByText("Choose Facts")).toBeVisible();
});
