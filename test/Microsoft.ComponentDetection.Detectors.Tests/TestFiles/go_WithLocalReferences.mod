module github.com/Go117Tests

go 1.23.5

replace github.com/grafana/grafana => ../../..

require (
	github.com/grafana/grafana v0.0.0-00010101000000-000000000000
	github.com/grafana/grafana-app-sdk v0.23.1
	k8s.io/apimachinery v0.32.0
	k8s.io/apiserver v0.32.0
	k8s.io/kube-openapi v0.0.0-20241105132330-32ad38e42d3f
)

replace (
	k8s.io/apimachinery => ../
	k8s.io/apiserver => ./a/b/c
)
