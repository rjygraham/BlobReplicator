# Helm chart for Let's Encrypt
A simple [helm](https://helm.sh/) chart for setting up the Let's Encrypt certificate cluster issuer in Kubernetes. This has been created using the documentation here: [Create an ingress controller with a static public IP address in Azure Kubernetes Service (AKS)](https://docs.microsoft.com/en-us/azure/aks/ingress-static-ip)

## Chart resources
This helm chart will deploy the following resources:
* Let's Encrypt certmanager `ClusterIssuer`
* Certificate Secret `Certificate`

### Prerequisites
* [Azure Subscription](https://azure.microsoft.com/)
* [Azure Kubernetes Service (AKS)](https://azure.microsoft.com/services/kubernetes-service/) or [ACS-Engine](https://github.com/Azure/acs-engine) deployment
* [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) (authenticated to your Kubernetes cluster)
* [Helm v1.10+](https://github.com/helm/helm)
* [Azure CLI 2.0](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
* [git](https://git-scm.com/downloads)

### Steps

1. Create an ingress controller

By default, an NGINX ingress controller is created with a new public IP address assignment. This public IP address is only static for the life-span of the ingress controller, and is lost if the controller is deleted and re-created. A common configuration requirement is to provide the NGINX ingress controller an existing static public IP address. The static public IP address remains if the ingress controller is deleted. This approach allows you to use existing DNS records and network configurations in a consistent manner throughout the lifecycle of your applications.

```bash
helm install stable/nginx-ingress \
    --namespace kube-system \
    --set controller.service.loadBalancerIP="XX.XX.XX.XX" \
    --set controller.replicaCount=1 \
    --set rbac.create=false
```
2. Install cert-manager

The NGINX ingress controller supports TLS termination. There are several ways to retrieve and configure certificates for HTTPS. This method demonstrates using cert-manager, which provides automatic Lets Encrypt certificate generation and management functionality.

```bash
kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.6/deploy/manifests/00-crds.yaml
kubectl create namespace cert-manager
kubectl label namespace cert-manager certmanager.k8s.io/disable-validation=true
helm repo update
helm install --name cert-manager \
    --namespace cert-manager \
    --version v0.6.0 stable/cert-manager \
    --set ingressShim.defaultIssuerName=letsencrypt-prod \
    --set ingressShim.defaultIssuerKind=ClusterIssuer --wait
```
