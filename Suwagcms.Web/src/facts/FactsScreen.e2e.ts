// A person works on the facts of one bot in the browser; the bot is in the address, and a refusal keeps what was typed.
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("facts");

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

test("AC-global-facts-1: a fact added in the web is listed with its type, limit, description and writableBy, which is set by default", async ({
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
    await expect(page.getByLabel("Description")).toHaveValue("Marta's text");
    // Why: the reload's answer shows nowhere on this screen, so Save is pressed until it is based on it.
    await expect(async () => {
        await page.getByRole("button", { name: "Save" }).click();
        await expect(page.getByRole("row").filter({ hasText: "customer.city" })).toContainText("Marta's text", { timeout: 1000 });
    }).toPass();
});

test("AC-global-facts-8: a fact deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    await page.goto(`/bots/${bot}/facts`);

    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await page
        .getByRole("alertdialog")
        .getByRole("button", { name: /^Delete/ })
        .click();

    await expect(page.getByText("No facts yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/facts/customer.city`)).status()).toBe(404);
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
    await expect(page.getByRole("region", { name: "Uses", exact: true })).toHaveCount(0);
    await usedBy.getByRole("link", { name: "orderValuation" }).click();
    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/orderValuation/edit$`));
    expect((await request.get(`/api/bots/${bot}/facts/customer.city`)).status()).toBe(200);
});

test("AC-edit-guard-11: a refused edit shows the mistake beside its field with a working link to the step, keeps what was typed, and AC-edit-guard-1: the fact stays", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const fact = await request.put(`/api/bots/${bot}/facts/order.kind?ifRevision=0`, {
        data: { type: "enum", description: "What is valued", values: ["house", "flat"] },
    });
    expect(fact.status()).toBe(200);
    const saved = await request.put(`/api/bots/${bot}/procedures/orderValuation?ifRevision=0`, {
        data: {
            description: "Orders a valuation.",
            firstStep: "ask",
            steps: {
                ask: { say: "Hi", next: [{ when: { "order.kind": { oneOf: ["flat"] } }, to: "end" }, { to: "end" }] },
                end: { say: "Bye" },
            },
        },
    });
    expect(saved.status()).toBe(200);

    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("row").filter({ hasText: "order.kind" }).getByRole("button", { name: "Edit" }).click();
    const values = page.getByLabel("Values (separated by commas)");
    await values.fill("house, land");
    await page.getByRole("button", { name: "Save" }).click();

    const field = page.locator('[data-field="values"]');
    await expect(field).toContainText("Value 'flat' of 'order.kind' is used, so it cannot be removed.");
    await expect(values).toHaveValue("house, land");
    const after = (await (await request.get(`/api/bots/${bot}/facts/order.kind`)).json()) as { values: string[]; revision: number };
    expect(after).toMatchObject({ values: ["house", "flat"], revision: 1 });

    const usedBy = page.getByRole("list", { name: "Used by" });
    await expect(usedBy).toContainText("step ask (condition)");
    await usedBy.getByRole("link", { name: "orderValuation" }).click();
    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/orderValuation/edit$`));
});

test("AC-resource-rename-5: Change id on a fact renames it, with dots, and opens it at its new name", async ({ page, request }) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();

    await page.getByRole("button", { name: "Change name" }).click();
    const dialog = page.getByRole("dialog", { name: "Change name" });
    await dialog.getByLabel("New name").fill("client.town");
    await expect(dialog).toContainText("This rewrites nothing else.");
    await dialog.getByRole("button", { name: "Change name" }).click();

    await expect(dialog).toBeHidden();
    await expect(page.locator('[data-field="name"] input')).toHaveValue("client.town");
    expect((await request.get(`/api/bots/${bot}/facts/customer.city`)).status()).toBe(404);
});

test("AC-fact-pattern-10: a person sets a pattern on a text fact, sees its mistakes beside the fields, and clears it again", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("button", { name: "Add a fact" }).click();
    await page.getByLabel("Name").fill("order.code");
    await page.getByLabel("Maximum length").fill("10");
    await page.getByLabel("Description").fill("The code");
    await page.getByLabel("Pattern: regular expression").fill("(?=a)b");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.locator('[data-field="pattern.regex"]')).toContainText("cannot be used");
    await expect(page.locator('[data-field="pattern.means"]')).toContainText("does not say what it means");
    await expect(page.getByLabel("Pattern: regular expression")).toHaveValue("(?=a)b");

    await page.getByLabel("Pattern: regular expression").fill("[A-Z]{2}");
    await page.getByLabel("Pattern: what it means").fill("two capitals");
    await page.getByLabel("Pattern: example").fill("ab");
    await page.getByRole("button", { name: "Add", exact: true }).click();
    await expect(page.locator('[data-field="pattern.example"]')).toContainText("does not match its regex");
    await expect(page.getByLabel("Pattern: regular expression")).toHaveValue("[A-Z]{2}");
    await expect(page.getByLabel("Pattern: what it means")).toHaveValue("two capitals");
    await expect(page.getByLabel("Pattern: example")).toHaveValue("ab");

    await page.getByLabel("Pattern: example").fill("AB");
    await page.getByLabel("Pattern: tell the customer").selectOption("afterRefusal");
    await page.getByRole("button", { name: "Add", exact: true }).click();
    const row = page.getByRole("row").filter({ hasText: "order.code" });
    await expect(row).toContainText("text, up to 10, pattern [A-Z]{2}");
    expect(await (await request.get(`/api/bots/${bot}/facts/order.code`)).json()).toMatchObject({
        pattern: { regex: "[A-Z]{2}", means: "two capitals", example: "AB", tellCustomer: "afterRefusal" },
    });

    await row.getByRole("button", { name: "Edit" }).click();
    await expect(page.getByLabel("Pattern: regular expression")).toHaveValue("[A-Z]{2}");
    await page.getByLabel("Pattern: regular expression").fill("");
    await page.getByLabel("Pattern: what it means").fill("");
    await page.getByLabel("Pattern: example").fill("");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(row).not.toContainText("pattern");
    expect(await (await request.get(`/api/bots/${bot}/facts/order.code`)).json()).toMatchObject({ pattern: null });
});

test("AC-global-facts-17, AC-resource-rename-28: unsaved changes ask before they are lost, and Change id waits for them", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addCity(request, bot);
    await page.goto(`/bots/${bot}/facts`);
    await page.getByRole("row").filter({ hasText: "customer.city" }).getByRole("button", { name: "Edit" }).click();
    await expect(page.getByRole("button", { name: "Change name" })).toBeEnabled();
    await page.getByLabel("Description").fill("Unsaved");
    const questions: string[] = [];
    let accept = false;
    page.on("dialog", (dialog) => {
        questions.push(dialog.message());
        if (accept) void dialog.accept();
        else void dialog.dismiss();
    });

    await expect(page.getByRole("button", { name: "Change name" })).toBeDisabled();
    await expect(page.getByText("Save your changes first.")).toBeVisible();
    await page.getByRole("button", { name: "Cancel" }).click();
    await page.getByRole("button", { name: "Back to facts" }).click();
    await page.getByRole("link", { name: "Bots" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts/customer.city/edit`);
    await expect(page.getByLabel("Description")).toHaveValue("Unsaved");
    accept = true;
    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Description")).toHaveCount(0);
    expect(questions).toEqual(Array(4).fill("Discard the changes you have not saved?"));
});
