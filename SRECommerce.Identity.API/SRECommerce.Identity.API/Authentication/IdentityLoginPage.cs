using System.Text.Encodings.Web;

namespace SRECommerce.Identity.API;

public static class IdentityLoginPage
{
    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
    public static string Render(string fieldName, string requestToken, string nonce, string returnUrl, bool mfa, bool error)
    {
        var fields = mfa
            ? "<label for=\"code\">Authenticator or recovery code</label><input id=\"code\" name=\"code\" autocomplete=\"one-time-code\" required autofocus><label class=\"check\"><input type=\"checkbox\" name=\"recovery\" value=\"true\"> Use a recovery code</label>"
            : "<label for=\"email\">Email</label><input id=\"email\" type=\"email\" name=\"email\" autocomplete=\"username\" maxlength=\"254\" required autofocus><label for=\"password\">Password</label><input id=\"password\" type=\"password\" name=\"password\" autocomplete=\"current-password\" maxlength=\"256\" required>";
        return $$$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>SRECommerce sign in</title><style nonce="{{{Encode(nonce)}}}">
        *{box-sizing:border-box}body{margin:0;background:#f3f5ef;color:#193027;font:16px/1.6 system-ui,sans-serif;min-height:100svh;display:grid;place-items:center;padding:2rem 1rem}
        main{width:100%;max-width:440px}.brand{display:flex;gap:.7rem;align-items:center;font-size:1.05rem;font-weight:750;margin-bottom:2rem}.mark{display:grid;place-items:center;width:39px;height:39px;border-radius:12px;background:#214b37;color:#fff;font:700 27px Georgia,serif}
        article{background:white;border:1px solid #d3ddcf;border-radius:18px;padding:2rem;box-shadow:0 12px 45px #19302708}.eyebrow{font-size:.65rem;text-transform:uppercase;letter-spacing:.18em;color:#5c725d;margin:0 0 .7rem}h1{font:2.25rem/1.15 Georgia,serif;margin:.4rem 0 1rem;letter-spacing:-.035em}
        p{color:#526558;font-size:.88rem}form{margin-top:1.6rem}label{display:block;font-size:.8rem;font-weight:600;margin:1.1rem 0 .4rem}input:not([type=hidden]):not([type=checkbox]){display:block;width:100%;min-height:47px;border:1px solid #9eafa1;border-radius:8px;padding:.7rem .8rem;font:inherit;background:#fff;color:#193027}
        input:focus-visible,button:focus-visible{outline:3px solid #a8c6a0;outline-offset:3px}button{width:100%;border:0;border-radius:8px;background:#214b37;color:white;font:600 .9rem system-ui;padding:.9rem;min-height:48px;margin-top:1.5rem;cursor:pointer}.check{display:flex;gap:.5rem;align-items:center;font-weight:400}.check input{width:18px;height:18px}
        .error{border-left:3px solid #ad3f2d;background:#fff2ed;color:#8a3021;padding:.75rem;border-radius:3px}.note{font-size:.75rem;line-height:1.7;padding:.5rem .4rem;color:#63725e}.fine{font-size:.7rem;color:#687c69;margin-top:1.4rem}
        @media(max-width:420px){article{padding:1.4rem}h1{font-size:2rem}body{padding:1.5rem 1rem}.brand{margin-bottom:1.4rem}}
        </style></head><body><main><div class="brand"><span class="mark" aria-hidden="true">S</span>SRECommerce.</div><article>
        <p class="eyebrow">Your secure account</p><h1>SRECommerce sign in</h1>
        {{{(error ? "<p class=\"error\" role=\"alert\">Unable to sign in. Verify the account, credentials and authenticator code.</p>" : "<p>Use your verified account to continue to your store or business workspace.</p>")}}}
        <form method="post" action="/account/login"><input type="hidden" name="{{{Encode(fieldName)}}}" value="{{{Encode(requestToken)}}}">
        <input type="hidden" name="returnUrl" value="{{{Encode(returnUrl)}}}"><input type="hidden" name="mfa" value="{{{mfa.ToString().ToLowerInvariant()}}}">
        {{{fields}}}<button type="submit">Sign in</button></form><p class="fine">Your store permissions are checked after sign-in.</p></article>
        <p class="note">New here or need to reset your password? Return to your store and choose Create an account to register, confirm your email or recover access.</p>
        </main></body></html>
        """;
    }
}
