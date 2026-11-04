using System.Net.Mail;
using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Application;

public sealed class CustomerAccountService(ICustomerAccountRepository store, ICartAccess access, TimeProvider clock)
{
    private async Task<Guid> User(Guid tenant, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ct);
        var actor = access.Actor ?? throw new BusinessFault("CustomerSignInRequired", 401);
        // Personal addresses and browsing history are never exposed through support impersonation.
        if (actor.SelectedTenantId != tenant || actor.TenantRole is null) throw new BusinessFault("TenantMembershipRequired", 403);
        return actor.UserId;
    }

    private static string Text(string? value, int maximum, bool required = true)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > maximum || required && text.Length == 0 || text.Any(c => char.IsControl(c) || c is '<' or '>'))
            throw new BusinessFault("InvalidAddress", 400);
        return text;
    }
    private static AddressView View(CustomerAddress row) => new(row.Id, row.Label, row.FullName, row.Email, row.Phone,
        row.AddressLine1, row.AddressLine2, row.City, row.CountryCode, row.PostalCode, Convert.ToBase64String(row.Version));

    public async Task<AddressView[]> Addresses(Guid tenant, CancellationToken ct) =>
        (await store.Addresses(await User(tenant, ct), ct)).Select(View).ToArray();

    public async Task<AddressView> SaveAddress(Guid tenant, Guid id, AddressInput input, CancellationToken ct)
    {
        var user = await User(tenant, ct);
        if (id == Guid.Empty) throw new BusinessFault("InvalidAddressId", 400);
        var label = Text(input.Label, 40); var name = Text(input.FullName, 160);
        var email = Text(input.Email, 254); var phone = Text(input.Phone, 25, false);
        var line1 = Text(input.AddressLine1, 200); var line2 = Text(input.AddressLine2, 200, false);
        var city = Text(input.City, 100); var country = Text(input.CountryCode, 2).ToUpperInvariant();
        var postal = Text(input.PostalCode, 16).ToUpperInvariant();
        if (!MailAddress.TryCreate(email, out var mail) || mail.Address != email ||
            !Regex.IsMatch(country, "^[A-Z]{2}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(postal, "^[A-Z0-9][A-Z0-9 -]{1,15}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(phone, "^[0-9+ -]*$", RegexOptions.CultureInvariant)) throw new BusinessFault("InvalidAddress", 400);
        return await store.Transaction(async token =>
        {
            var row = await store.Address(user, id, token);
            if (row is null)
            {
                if (input.Version is not null) throw new BusinessFault("VersionConflict");
                if ((await store.Addresses(user, token)).Length >= 10) throw new BusinessFault("AddressLimitReached");
                row = new() { TenantId = tenant, UserId = user, Id = id }; store.Add(row);
            }
            else
            {
                if (!row.Active) throw new BusinessFault("AddressNotFound", 404);
                store.Version(row, input.Version ?? throw new BusinessFault("VersionRequired", 400));
            }
            row.Label = label; row.FullName = name; row.Email = email; row.Phone = phone.Length == 0 ? null : phone;
            row.AddressLine1 = line1; row.AddressLine2 = line2.Length == 0 ? null : line2; row.City = city;
            row.CountryCode = country; row.PostalCode = postal; row.UpdatedAt = clock.GetUtcNow();
            await store.Save("customer.address.saved", id, token); return View(row);
        }, ct);
    }

    public async Task ArchiveAddress(Guid tenant, Guid id, AddressVersion input, CancellationToken ct)
    {
        var user = await User(tenant, ct);
        await store.Transaction(async token =>
        {
            var row = await store.Address(user, id, token) ?? throw new BusinessFault("AddressNotFound", 404);
            if (!row.Active) return true;
            store.Version(row, input.Version); row.Active = false; row.UpdatedAt = clock.GetUtcNow();
            await store.Save("customer.address.archived", id, token); return true;
        }, ct);
    }

    private static void Kind(SavedProductKind kind) { if (!Enum.IsDefined(kind)) throw new BusinessFault("InvalidSavedProductKind", 400); }
    private static SavedProductView View(SavedProduct row, CartCatalogProduct? current) => new(row.ProductId, row.Kind,
        current is not null, current?.Name, current?.Slug, row.UpdatedAt, Convert.ToBase64String(row.Version));

    public async Task<SavedProductPage> Saved(Guid tenant, SavedProductKind kind, int page, CancellationToken ct)
    {
        var user = await User(tenant, ct); Kind(kind);
        if (page is < 1 or > 4) throw new BusinessFault("InvalidSavedProductsPage", 400);
        var rows = await store.Saved(user, kind, page, ct);
        var visible = rows.Take(25).ToArray();
        var current = visible.Length == 0 ? [] : await access.PublishedProducts(tenant, visible.Select(row => row.ProductId).ToArray(), ct);
        var lookup = current.ToDictionary(row => row.Id);
        return new(visible.Select(row => View(row, lookup.GetValueOrDefault(row.ProductId))).ToArray(), page, rows.Length > 25);
    }

    public async Task<SavedProductView> SaveProduct(Guid tenant, SavedProductKind kind, Guid product, SavedProductInput input, CancellationToken ct)
    {
        var user = await User(tenant, ct); Kind(kind);
        if (product == Guid.Empty || kind == SavedProductKind.RecentlyViewed && !input.Active) throw new BusinessFault("InvalidSavedProduct", 400);
        var current = input.Active ? (await access.PublishedProducts(tenant, [product], ct)).SingleOrDefault(row => row.Id == product)
            ?? throw new BusinessFault("ProductNotAvailable", 404) : null;
        return await store.Transaction(async token =>
        {
            var row = await store.Saved(user, kind, product, token);
            var wasActive = row?.Active == true;
            if (row is null)
            {
                if (!input.Active || input.Version is not null) throw new BusinessFault("SavedProductNotFound", 404);
                row = new() { TenantId = tenant, UserId = user, ProductId = product, Kind = kind }; store.Add(row);
            }
            else if (kind == SavedProductKind.Wishlist)
            {
                if (input.Version is null && row.Active && input.Active) return View(row, current);
                if (input.Version is not null) store.Version(row, input.Version);
                else if (!input.Active) throw new BusinessFault("VersionRequired", 400);
            }
            if (kind == SavedProductKind.Wishlist && input.Active && !wasActive && await store.Count(user, kind, token) >= 100)
                throw new BusinessFault("WishlistLimitReached");
            row.Active = input.Active; row.UpdatedAt = clock.GetUtcNow();
            await store.Save(kind == SavedProductKind.Wishlist ? "customer.wishlist.saved" : "customer.recently-viewed.saved", product, token);
            if (kind == SavedProductKind.RecentlyViewed) { await store.TrimRecent(user, token); await store.Save("customer.recently-viewed.trimmed", product, token); }
            return View(row, current);
        }, ct);
    }

    public async Task ClearRecent(Guid tenant, CancellationToken ct)
    {
        var user = await User(tenant, ct);
        await store.Transaction(async token =>
        {
            var rows = await store.Saved(user, SavedProductKind.RecentlyViewed, 1, token);
            // The repository retains at most 25 active recent records.
            foreach (var row in rows) row.Active = false;
            await store.Save("customer.recently-viewed.cleared", user, token); return true;
        }, ct);
    }
}
