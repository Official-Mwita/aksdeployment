

# Deploying Microservices into Azure AKS
## Prerequisites
- AZ CLI version ^2.0.53
- [Kubernetes CLI](#install-kubectl)
- Docker
- [Helm](https://helm.sh)

## Assumptions
This deployment was made over the following assumptions
- Active Azure subscription
- AZ CLI version ^2.48.1
- Docker installed on WSL
- A bash terminal is used
- Won't exit bash session until complete
- 2 Dotnet applications (microservices) in one solution. And their docker files prepared
**Main Reference:** [Source](https://learn.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-app)

## 1. Build and Configure Images Using Docker Compose
Start docker desktop
Open terminal in the `main` folder. alternative cd to the terminal
Test microservices locally by deploying them in docker
```
docker compose up
```
*The process may take several minutes in the first time of deployment*

Clean up resources. Ready for AKS deployment
```
docker compose down
```

## AKS Deployment Using Bash AZ CLI
1. Authenticate AZ CLI:
   ```
   az account --use-device-code
   ```
2. Create a resource group:
   ```
   rg=ResourceName
   az group create --name $rg --location eastus
   ```

3. Create an Azure Container Registry (ACR):
   ```
   acr=YourACRpreferedanduniquename
   az acr create --resource-group $rg --name $acr --sku Basic
   ```

4. Authenticate Docker to the created ACR:
   ```
   az acr login --name $acr
   ```

5. Tag local docker images to your ACR:
   Confirm availability of all microservices docker images
   [image]
   
   Obtain ACR registry url. This command assumes that you only have ACR or the acr targeted is first in the display 
   ```
   acrdomain=$(az acr list --resource-group $rg --query "[].{acrLoginServer:loginServer}" --output table | awk 'NR==3{print $1}')
   docker tag <localimagename> $acrdomain/<localimagename>:<versioning>
   ```

7. Confirm tagged images:
   ```
   docker images
   ```
   [images]

8. Push images to the Azure repository:
   ```
   docker push $acrdomain/<localimagename>:<versioning>
   ```

9. Verify pushed images:
   ```
   az acr repository list --name $acr --output table
   ```

## 2.  Create and Deploy Azure Kubernetes Cluster (AKS)

1. Install kubectl to AZ CLI:
   ```
   az aks install-cli
   ```

2. Create an AKS cluster:
   ```
   cluster=exclusiveCluster
   az aks create \
   --resource-group $rg \
   --name $cluster \
   --node-count 1 \
   --attach-acr $acr
   ```

3. Install Unmanaged Ingress Controller:

   - Helm3 Installation: [Install Helm](https://helm.sh/docs/intro/install/)

   - Import images used by Helm chart into ACR:
     ```
     REGISTRY_NAME=$acr
     SOURCE_REGISTRY=registry.k8s.io
     CONTROLLER_IMAGE=ingress-nginx/controller
     CONTROLLER_TAG=v1.2.1
     PATCH_IMAGE=ingress-nginx/kube-webhook-certgen
     PATCH_TAG=v1.1.1
     DEFAULTBACKEND_IMAGE=defaultbackend-amd64
     DEFAULTBACKEND_TAG=1.5

     az acr import --name $REGISTRY_NAME --source $SOURCE_REGISTRY/$CONTROLLER_IMAGE:$CONTROLLER_TAG --image $CONTROLLER_IMAGE:$CONTROLLER_TAG
     az acr import --name $REGISTRY_NAME --source $SOURCE_REGISTRY/$PATCH_IMAGE:$PATCH_TAG --image $PATCH_IMAGE:$PATCH_TAG
     az acr import --name $REGISTRY_NAME --source $SOURCE_REGISTRY/$DEFAULTBACKEND_IMAGE:$DEFAULTBACKEND_TAG --image $DEFAULTBACKEND_IMAGE:$DEFAULTBACKEND_TAG
     ```

   - Add the ingress-nginx repository:
     ```
     helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
     helm repo update
     ```
   -Authenticate helm (Kubectl) into your cluster environment 
   
     ```
     az aks get-credentials -g $rg -n $cluster
     ```
     - Install Ingress using Helm:
     ```
     helm install ingress-nginx ingress-nginx/ingress-nginx \
         --version 4.1.3 \
         --namespace default \
         --create-namespace \
         --set controller.replicaCount=2 \
         --set controller.nodeSelector."kubernetes\.io/os"=linux \
         --set controller.image.registry==$acrdomain \
         --set controller.image.image=$CONTROLLER_IMAGE \
         --set controller.image.tag=$CONTROLLER_TAG \
         --set controller.image.digest="" \
         --set controller.admissionWebhooks.patch.nodeSelector."kubernetes\.io/os"=linux \
         --set controller.service.loadBalancerIP=10.224.0.42 \
         --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-internal"=false \
         --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz \
         --set controller.admissionWebhooks.patch.image.registry==$acrdomain \
         --set controller.admissionWebhooks.patch.image.image=$PATCH_IMAGE \
         --set controller.admissionWebhooks.patch.image.tag=$PATCH_TAG \
         --set controller.admissionWebhooks.patch.image.digest="" \
         --set defaultBackend.nodeSelector."kubernetes\.io/os"=linux \
         --set defaultBackend.image.registry==$acrdomain \
         --set defaultBackend.image.image=$DEFAULTBACKEND_IMAGE \
         --set defaultBackend.image.tag=$DEFAULTBACKEND_TAG \
         --set defaultBackend.image.digest=""
     ```
**The process will take a few minutes to set ingress proxy into your cluster**
<p>Note: The ingress server may be automatically assigned a public IP address. If not you can assign it manually by following the process.<br>To check if it was assigned. Run the following command</p>

```
kubectl get services --namespace [yournamespace] -o wide -w [youringressname]
```
*Expected output for the command above*
```
NAME                       TYPE           CLUSTER-IP    EXTERNAL-IP     PORT(S)                      AGE   SELECTOR
ingress-nginx-controller   LoadBalancer   10.0.65.205   EXTERNAL-IP     80:30957/TCP,443:32414/TCP   1m   app.kubernetes.io/component=controller,app.kubernetes.io/instance=ingress-nginx,app.kubernetes.io/name=ingress-nginx
```

<p>Otherwise you can manually obtain a public IP address, and then assign your ingress</p>

### Obtaining a public IP address

- Obtain a resource group for your cluster into a shell variable. This is where you will create the proxy server's public IP
  ```
  netrsrc=$(az aks show --resource-group $rg --name $cluster --query nodeResourceGroup -o tsv)
  ```
- Create an IP address
  ```
  ip=$(az network public-ip create --resource-group $netrsrc --name <myAKSPublicIP> --sku Standard --allocation-method static --query publicIp.ipAddress -o tsv)
  ```
- Apply the IP to your ingress server
  ```
  helm upgrade <ingres-name> ingress-nginx/ingress-nginx \
  --namespace <yournamespace> \
  --set controller.service.loadBalancerIP=$ip \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz
  ```
-- Configure FQDN to your IP address
   - For Azure FQDN - browse to IP address resource, add it's FQDN
   - If using an external domain (custom domain). Browse to your domain DNS admin portal, then add you preferred DNS record e.g CNAME, AAAA, etc

**Try to access your server from the internet to confirm that it's working - either by IP address or domain. You should get a 404 Nginx default response**

## Setting up TSL (SSL encryption) - using Let's Encrypt
- Installing a [Cert-Manager](https://github.com/cert-manager/cert-manager)
  [Cert-Manager](https://github.com/cert-manager/cert-manager) is an open-source SSL configuration bot provided under [Apache 2.0 license](https://www.apache.org/licenses/LICENSE-2.0) used to automate Kubernetes cluster's SSL certificates acquisition
     - Label the ingress-basic namespace to disable resource validation
       ```
       kubectl label namespace ingress-basic cert-manager.io/disable-validation=true
       ```
     - Add the Jetstack Helm repository
       ```
       helm repo add jetstack https://charts.jetstack.io
       ```
     - Update your local Helm chart repository cache
       ```
       helm repo update
       ```
- Add cert-manager images to your ACR
   ```
   REGISTRY_NAME=$acr
   CERT_MANAGER_REGISTRY=quay.io
   CERT_MANAGER_TAG=v1.8.0
   CERT_MANAGER_IMAGE_CONTROLLER=jetstack/cert-manager-controller
   CERT_MANAGER_IMAGE_WEBHOOK=jetstack/cert-manager-webhook
   CERT_MANAGER_IMAGE_CAINJECTOR=jetstack/cert-manager-cainjector
   ```
   ```
   az acr import --name $REGISTRY_NAME --source $CERT_MANAGER_REGISTRY/$CERT_MANAGER_IMAGE_CONTROLLER:$CERT_MANAGER_TAG --image $CERT_MANAGER_IMAGE_CONTROLLER:$CERT_MANAGER_TAG
   ```
   ```
   az acr import --name $REGISTRY_NAME --source $CERT_MANAGER_REGISTRY/$CERT_MANAGER_IMAGE_WEBHOOK:$CERT_MANAGER_TAG --image $CERT_MANAGER_IMAGE_WEBHOOK:$CERT_MANAGER_TAG
   ```
   ```
   az acr import --name $REGISTRY_NAME --source $CERT_MANAGER_REGISTRY/$CERT_MANAGER_IMAGE_CAINJECTOR:$CERT_MANAGER_TAG --image $CERT_MANAGER_IMAGE_CAINJECTOR:$CERT_MANAGER_TAG
   ```
- Install the cert-manager Helm chart
  ```
  helm install cert-manager jetstack/cert-manager \
  --namespace <namespace> \
  --version=$CERT_MANAGER_TAG \
  --set installCRDs=true \
  --set nodeSelector."kubernetes\.io/os"=linux \
  --set image.repository=$ACR_URL/$CERT_MANAGER_IMAGE_CONTROLLER \
  --set image.tag=$CERT_MANAGER_TAG \
  --set webhook.image.repository=$ACR_URL/$CERT_MANAGER_IMAGE_WEBHOOK \
  --set webhook.image.tag=$CERT_MANAGER_TAG \
  --set cainjector.image.repository=$ACR_URL/$CERT_MANAGER_IMAGE_CAINJECTOR \
  --set cainjector.image.tag=$CERT_MANAGER_TAG
  ```

## Create a CA cluster issuer
Add CA cluster issuer to your kubernetes cluster - preferably yaml

## Deploy Images to AKS

- Prerequisite: Have deployment files ready.
- Deploy them as ClusterIP services:
  ```
  kubectl apply -f deploy.yaml
  ```

