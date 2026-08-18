# CsvShuffle

## Releases and versioning

`CsvShuffle/Version.props` is the single release-version source. Change its `Version`
value (for example, from `1.0.0` to `1.1.0`) in a pull request. When that pull
request is merged into `main`, the publish workflow automatically:

- creates a GitHub release and `v<version>` tag;
- publishes `ghcr.io/<owner>/csvshuffle:<version>`;
- packages and publishes the Helm chart at the same version, with the same
  `appVersion` and default Kubernetes image tag.

The application header also reads that build version, rather than maintaining a
separate display value. A chart consumer can still set `image.tag` explicitly to
deploy a different image.

If the version has not changed, the workflow completes without republishing it.
