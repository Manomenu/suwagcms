// A person works on the facts of one bot in the browser; the bot is in the address, and a refusal keeps what was typed.
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function freshId(): string {
    return `facts-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function addCity(request: APIRequestContext, bot: string, description = "The city the customer lives in"): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/facts/customer.city?ifRevision=0`, {
        data: { type: "text", description, writableBy: null, values: null, min: null, max: null, maxLength: 80 },
    });
    expect(response.status()).toBe(200);
}

async function addIn(page: Page, name: string, description: string, maxLength: string): Promise<void> {
    await page.getByRole("button", { name: "Add a fact" }).click();
    await page.getByLabel("Name").fill(name);
    await page.getByLabel("Maximum length").fill(maxLength);
    await page.getByLabel("Description").fill(description);
    await page.getByRole("button", { name: "Add", exact: true }).click();
}

test("AC-global-facts-1: a fact added in the web is listed with its type, limit, description and writableBy, which is set by default (AC-global-facts-2)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/facts`);

    await addIn(page, "customer.city", "The city the customer lives in", "80");

    const row = page.getByRole("row").filter({ hasText: "customer.city" });
    await expect(row).toContainText("text, up to 80 · set");
    await expect(row).toContainText("The city the customer lives in");
    const listed = await request.get(`/api/bots/${bot}/facts`);
    expect(await listed.json()).toMatchObject([{ name: "customer.city", type: "text", maxLength: 80, writableBy: "set" }]);
});

test("AC-global-facts-6: the web shows the mistakes of a bad fact beside their fields, the same the API gives", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const api = await request.put(`/api/bots/${bot}/facts/order.kind?ifRevision=0`, {
        data: { type: "enum", description: "", writableBy: null, values: ["house"], min: null, max: null, maxLength: null },
    });
    expect(api.status()).toBe(422);
    const refusal = (await api.json()) as { mistakes: { code: string; path: string; message: string; fix: string }[] };
    const mistakes = refusal.mistakes;
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("button", { name: "Add a fact" }).click();
    await page.getByLabel("Name").fill("order.kind");
    await page.getByLabel("Type").selectOption("enum");
    await page.getByLabel("Values").fill("house");

    await page.getByRole("button", { name: "Add", exact: true }).click();

    expect(mistakes.map((mistake) => mistake.code)).toEqual(["fact.description.missing", "fact.enum.tooFewValues"]);
    for (const mistake of mistakes) {
        await expect(page.locator(`[data-field="${mistake.path}"]`)).toContainText(`${mistake.message} ${mistake.fix}`);
    }
    await expect(page.getByLabel("Values")).toHaveValue("house");
    expect((await (await request.get(`/api/bots/${bot}/facts`)).json()) as unknown[]).toEqual([]);
});

test("AC-global-facts-4: adding a name the bot already has shows the refusal beside the name", async ({ page, request }) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    await page.goto(`/bots/${bot}/facts`);

    await addIn(page, "customer.city", "Another", "10");

    await expect(page.locator('[data-field="name"]')).toContainText("already exists");
    await expect(page.getByLabel("Description")).toHaveValue("Another");
});

test("AC-global-facts-11: saving an edit of a fact that changed meanwhile is refused with the current revision and keeps what was typed", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addCity(request, bot, "First");
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Description").fill("Marta's text");
    const other = await request.put(`/api/bots/${bot}/facts/customer.city?ifRevision=1`, {
        data: { type: "text", description: "The agent's text", writableBy: null, values: null, min: null, max: null, maxLength: 80 },
    });
    expect(other.status()).toBe(200);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel("Description")).toHaveValue("Marta's text");
    await page.getByRole("button", { name: "Reload the facts" }).click();
    await expect(page.getByRole("row").filter({ hasText: "customer.city" })).toContainText("The agent's text");
    await expect(page.getByLabel("Description")).toHaveValue("Marta's text");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("row").filter({ hasText: "customer.city" })).toContainText("Marta's text");
});

test("AC-global-facts-8: a fact deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    await page.goto(`/bots/${bot}/facts`);

    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No facts yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/facts/customer.city`)).status()).toBe(404);
});

test("AC-global-facts-13: the facts screen of one bot shows nothing of another bot's facts", async ({ page, request }) => {
    const first = await newBot(request);
    const second = await newBot(request);
    await addCity(request, first);

    await page.goto(`/bots/${second}/facts`);

    await expect(page.getByText("No facts yet.")).toBeVisible();
    await expect(page.getByText("customer.city")).toHaveCount(0);
});

test("AC-delete-guard-12: a fact a procedure uses is refused for delete in the API, and in the web Delete is disabled beside a link to the procedure (AC-delete-guard-7, AC-delete-guard-9)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    const saved = await request.put(`/api/bots/${bot}/procedures/orderValuation?ifRevision=0`, {
        data: { description: "Orders a valuation.", firstStep: "ask", steps: { ask: { collect: ["customer.city"] } } },
    });
    expect(saved.status()).toBe(200);

    const refused = await request.delete(`/api/bots/${bot}/facts/customer.city`);
    expect(refused.status()).toBe(422);
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("button", { name: "Delete" })).toBeDisabled();
    await expect(page.getByText("Used in 1 place. Remove that reference first.")).toBeVisible();
    const usedBy = page.getByRole("region", { name: "Used by" });
    await expect(usedBy).toContainText("steps.ask.");
    await usedBy.getByRole("link", { name: "orderValuation" }).click();
    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/orderValuation$`));
    expect((await request.get(`/api/bots/${bot}/facts/customer.city`)).status()).toBe(200);
});
