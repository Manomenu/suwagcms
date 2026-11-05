// A person works on the actions of one bot in the browser; the CMS never calls an action's URL, and a refusal keeps what was typed.
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function freshId(): string {
    return `actions-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function typeBasics(page: Page, id: string, url: string): Promise<void> {
    await page.getByRole("button", { name: "Add an action" }).click();
    await page.getByLabel("Id").fill(id);
    await page.getByLabel("Description", { exact: true }).first().fill("Checks whether we serve an address.");
    await page.getByLabel("URL").fill(url);
}

test("AC-http-actions-1: an action added in the web with parameters, results, headers and a timeout reads back as written", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/actions`);

    await typeBasics(page, "checkServiceability", "https://crm.internal/serviceability");
    await page.getByLabel("Timeout (s)").fill("20");
    await page.getByRole("button", { name: "Add a header" }).click();
    await page.getByLabel("Header", { exact: true }).fill("Authorization");
    await page.getByLabel("Value", { exact: true }).fill("Bearer {secret:CRM_TOKEN}");
    await page.getByRole("button", { name: "Add a parameter" }).click();
    const parameter = page.getByRole("group", { name: "Parameters" });
    await parameter.getByLabel("Name").fill("address");
    await parameter.getByLabel("Maximum length").fill("200");
    await parameter.getByLabel("Description").fill("Street and city.");
    await page.getByRole("button", { name: "Add a result" }).click();
    const result = page.getByRole("group", { name: "Results" });
    await result.getByLabel("Name").fill("serviceable");
    await result.getByLabel("Type").selectOption("enum");
    await result.getByLabel("Values").fill("yes, no");
    await result.getByLabel("Description").fill("Whether we serve it.");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "checkServiceability" });
    await expect(row).toContainText("http");
    const shown: unknown = await (await request.get(`/api/bots/${bot}/actions/checkServiceability`)).json();
    expect(shown).toMatchObject({
        parameters: { address: { type: "text", maxLength: 200 } },
        results: { serviceable: { type: "enum", values: ["yes", "no"] } },
        implementation: {
            http: {
                method: "POST",
                url: "https://crm.internal/serviceability",
                headers: { Authorization: "Bearer {secret:CRM_TOKEN}" },
                timeoutSeconds: 20,
            },
        },
    });

    await row.getByRole("button", { name: "Open" }).click();
    await expect(page.getByLabel("Value", { exact: true })).toHaveValue("Bearer {secret:CRM_TOKEN}");
    await expect(page.getByLabel("URL")).toHaveValue("https://crm.internal/serviceability");
    await expect(page.getByLabel("Timeout (s)")).toHaveValue("20");
});

test("AC-http-actions-8: the web shows the mistakes of a bad action beside the fields their paths name, the same the API gives", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const api = await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks whether we serve an address.",
            parameters: null,
            results: null,
            implementation: {
                http: { method: "POST", url: "ftp://crm.internal", headers: { Authorization: "plain" }, timeoutSeconds: null },
            },
        },
    });
    expect(api.status()).toBe(422);
    const { mistakes } = (await api.json()) as { mistakes: { path: string; message: string; fix: string }[] };
    await page.goto(`/bots/${bot}/actions`);

    await typeBasics(page, "check", "ftp://crm.internal");
    await page.getByRole("button", { name: "Add a header" }).click();
    await page.getByLabel("Header", { exact: true }).fill("Authorization");
    await page.getByLabel("Value", { exact: true }).fill("plain");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    expect(mistakes.length).toBe(2);
    for (const mistake of mistakes) {
        await expect(page.locator(`[data-field="${mistake.path}"]`)).toContainText(`${mistake.message} ${mistake.fix}`);
    }
    await expect(page.getByLabel("URL")).toHaveValue("ftp://crm.internal");
    expect((await (await request.get(`/api/bots/${bot}/actions`)).json()) as unknown[]).toEqual([]);
});

test("AC-http-actions-7: a saved action shows its secrets only as names on the page", async ({ page, request }) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: null,
            results: null,
            implementation: {
                http: { method: "GET", url: "https://x.internal", headers: { "X-Token": "{secret:CRM_TOKEN}" }, timeoutSeconds: null },
            },
        },
    });
    await page.goto(`/bots/${bot}/actions?open=check`);

    await expect(page.getByLabel("Value", { exact: true })).toHaveValue("{secret:CRM_TOKEN}");
    await expect(page.getByText("The CMS never holds its value.")).toBeVisible();
});

test("AC-http-actions-11: an unused action can be deleted in the web and is gone", async ({ page, request }) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: null,
            results: null,
            implementation: { http: { method: "GET", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await page.goto(`/bots/${bot}/actions?open=check`);

    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No actions yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/actions/check`)).status()).toBe(404);
});
