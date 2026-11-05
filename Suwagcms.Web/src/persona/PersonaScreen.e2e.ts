import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function freshId(): string {
    return `persona-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function openPersona(page: Page, bot: string): Promise<void> {
    await page.goto(`/bots/${bot}/persona`);
    await expect(page.getByRole("heading", { name: `Persona of ${bot}` })).toBeVisible();
}

test("AC-bot-persona-1: Enter gives a line break, and each text reads back as written after a reload", async ({ page, request }) => {
    const bot = await newBot(request);
    await openPersona(page, bot);
    await expect(page.getByRole("navigation", { name: "Bots" }).getByRole("link", { name: "Persona", exact: true })).toHaveAttribute(
        "aria-current",
        "page",
    );

    const personality = page.getByLabel("Personality");
    await personality.click();
    await page.keyboard.type("First line");
    await page.keyboard.press("Enter");
    await page.keyboard.type("  Second line");
    await page.getByLabel("When out of scope").fill("Decline\nOffer help\n");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByText("Saved", { exact: true })).toBeVisible();
    await expect(page.getByText("revision 1").first()).toBeVisible();
    expect(await (await request.get(`/api/bots/${bot}/persona`)).json()).toMatchObject({
        personality: "First line\n  Second line",
        scope: { whenOutOfScope: "Decline\nOffer help\n" },
    });

    await page.reload();
    await expect(page.getByLabel("Personality")).toHaveValue("First line\n  Second line");
    await expect(page.getByLabel("When out of scope")).toHaveValue("Decline\nOffer help\n");
});

test("AC-bot-persona-6: a save on an old revision is refused with the current one, the typed text stays, and a reload shows the other save", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await openPersona(page, bot);
    await page.getByLabel("Personality").fill("Marta's words");

    const other = await request.put(`/api/bots/${bot}/persona`, { data: { personality: "Piotr's words" } });
    expect(other.status()).toBe(200);
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("revision 1");
    await expect(page.getByLabel("Personality")).toHaveValue("Marta's words");

    await page.getByRole("button", { name: "Reload the persona" }).click();
    await expect(page.getByLabel("Personality")).toHaveValue("Piotr's words");
    await expect(page.getByRole("alert")).toHaveCount(0);
});

test("AC-bot-persona-4: a text above the limit is refused beside its field and nothing is saved", async ({ page, request }) => {
    const bot = await newBot(request);
    await openPersona(page, bot);
    const tooLong = "x".repeat(20_001);
    await page.locator("#persona-outOfScope").fill(tooLong);
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.locator("#persona-outOfScope-mistakes")).toContainText("20000");
    await expect(page.locator("#persona-outOfScope")).toHaveValue(tooLong);
    expect(await (await request.get(`/api/bots/${bot}/persona`)).json()).toMatchObject({ revision: 0 });
});

test("AC-bot-persona-1: leaving with unsaved text asks first", async ({ page, request }) => {
    const bot = await newBot(request);
    await openPersona(page, bot);
    await page.getByLabel("Personality").fill("Not saved yet");

    const asked: string[] = [];
    page.on("dialog", (dialog) => {
        asked.push(dialog.message());
        void dialog.dismiss();
    });
    await page.getByRole("navigation", { name: "Bots" }).getByRole("link", { name: "Facts", exact: true }).click();

    expect(asked).toHaveLength(1);
    expect(asked[0]).toContain("not saved");
    await expect(page.getByLabel("Personality")).toHaveValue("Not saved yet");
});
