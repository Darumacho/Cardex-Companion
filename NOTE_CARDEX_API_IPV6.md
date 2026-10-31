# Note — appels à cardex-api.dev bloqués ~75 s (IPv6)

Diagnostic fait le 2026-10-31 sur le Mac (portage macOS). Concerne aussi Windows. À traiter côté
hébergement de l'API et/ou dans `Services/CardexApiService.cs`.

## Symptôme

- Au démarrage, l'app reste sur « Welcome to Cardex » / « Loading sets… » pendant plusieurs
  minutes : « plus rien ne charge ».
- Depuis la 3.2, le bouton ↻ (recharger un set) passe d'abord par l'API Cardex : le premier
  rechargement de la session subit le même délai.

## Cause

Le DNS de `cardex-api.dev` publie deux adresses :

| Type | Adresse | Résultat |
|---|---|---|
| A (IPv4) | `51.254.208.117` | répond — `curl -4` : HTTP 200 en 0,18 s |
| AAAA (IPv6) | `2001:41d0:305:2100::1` | ne répond pas — `curl -6` : délai dépassé après 10 s |

Le `HttpClient` .NET par défaut tente d'abord l'IPv6, attend l'expiration de la connexion
(~75 s sur macOS), puis seulement l'IPv4. Mesure avec un client identique à celui de
`CardexApiService` : **`GET /api/sets` réussi, mais en 75,4 s**.

Les appels concernés sont faits au démarrage, **avant** le chargement des sets, par la synchro
hebdomadaire du catalogue (`DiscoverNewCardexEntriesAsync` + `SyncFromCardexApiAsync`, relancée
si `LastCardexSyncAt` a plus de 7 jours). Chaque nouvelle connexion coûte ~75 s : l'app paraît
figée.

`CollectionSyncService` contourne déjà ce bug : son `SocketsHttpHandler` force la résolution en
IPv4 (`ConnectCallback`). `CardexApiService` n'a pas ce contournement.

## Correctifs possibles

1. **Hébergement (vraie correction)** : supprimer l'enregistrement AAAA de `cardex-api.dev`, ou le
   faire pointer vers une adresse IPv6 réellement servie (vérifier que le serveur web écoute en
   IPv6 et que le pare-feu l'autorise).
   Vérification : `curl -6 -I https://cardex-api.dev/` doit répondre immédiatement.
2. **Côté app, immédiat** : donner à `CardexApiService` le même `SocketsHttpHandler` forcé en IPv4
   que `CollectionSyncService` — idéalement via un petit helper commun aux deux services.
   Fichier partagé : la correction vaut pour Windows et macOS.
3. **Garde-fou** : même une fois réparé, un timeout court sur ces appels (ex. 10 s, comme
   `CollectionSyncService`) éviterait qu'une panne de l'API bloque le démarrage pendant des
   minutes. Le `HttpClient` de `CardexApiService` garde aujourd'hui le timeout par défaut (100 s).

## Pour reproduire

```sh
curl -4 -s -o /dev/null -w "%{http_code} en %{time_total}s\n" https://cardex-api.dev/
curl -6 -s -o /dev/null -m 10 -w "%{http_code} en %{time_total}s\n" https://cardex-api.dev/
```

Sous Windows (PowerShell) : `Resolve-DnsName cardex-api.dev`, puis
`Test-NetConnection 2001:41d0:305:2100::1 -Port 443`.
