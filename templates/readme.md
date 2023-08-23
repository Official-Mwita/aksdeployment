Sure, I can help you convert the provided text into a proper Markdown file. Here's the formatted text in Markdown:

```markdown
# Azure AKS Tutorial

**Source:** [Azure AKS Tutorial](https://learn.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-app)

## Build and Configure Images Using Docker Compose

- Test images locally using Docker Compose:
  ```
  docker compose up
  ```
- Clean up resources:
  ```
  docker compose down
  ```

## AKS Deployment Using Bash AZ CLI

1. Create a resource group:
   ```
   rg=ResourceName
   az group create --name $rg --location eastus
   ```

2. Authenticate AZ CLI:
   ```
   az account --use-device-code
   ```

3. Create an Azure Container Registry (ACR):
   ```
   acr=YourACRpreferedanduniquename
   az acr create --resource-group $rg --name $acr --sku Basic
   ```

4. Authenticate Docker to the created ACR:
   ```
   az acr login --name dockerhosexyz
   ```

5. Tag local docker images to your ACR:
   ```
   acrdomain=$(az acr list --resource-group dockerTest --query "[].{acrLoginServer:loginServer}" --output table | awk 'NR==3{print $1}')
   docker tag <localimagename> $acrdomain/<localimagename>:<versioning>
   ```

6. Confirm tagged images:
   ```
   docker images
   ```

7. Push images to the Azure repository:
   ```
   docker push $acrdomain/<localimagename>:<versioning>
   ```

8. Verify pushed images:
   ```
   az acr repository list --name $acr --output table
   ```

## Create and Deploy Azure Kubernetes Cluster (AKS)

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

   - Set variable for ACR location:
     ```
     ACR_URL=$acrdomain
     ```

   - Install Ingress using Helm:
     ```
     az aks get-credentials -g $rg -n $cluster
     helm install ingress-nginx ingress-nginx/ingress-nginx \
         --version 4.1.3 \
         --namespace default \
         --create-namespace \
         --set controller.replicaCount=2 \
         --set controller.nodeSelector."kubernetes\.io/os"=linux \
         --set controller.image.registry=$ACR_URL \
         --set controller.image.image=$CONTROLLER_IMAGE \
         --set controller.image.tag=$CONTROLLER_TAG \
         --set controller.image.digest="" \
         --set controller.admissionWebhooks.patch.nodeSelector."kubernetes\.io/os"=linux \
         --set controller.service.loadBalancerIP=10.224.0.42 \
         --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-internal"=false \
         --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz \
         --set controller.admissionWebhooks.patch.image.registry=$ACR_URL \
         --set controller.admissionWebhooks.patch.image.image=$PATCH_IMAGE \
         --set controller.admissionWebhooks.patch.image.tag=$PATCH_TAG \
         --set controller.admissionWebhooks.patch.image.digest="" \
         --set defaultBackend.nodeSelector."kubernetes\.io/os"=linux \
         --set defaultBackend.image.registry=$ACR_URL \
         --set defaultBackend.image.image=$DEFAULTBACKEND_IMAGE \
         --set defaultBackend.image.tag=$DEFAULTBACKEND_TAG \
         --set defaultBackend.image.digest=""
     ```

## Deploy Images to AKS

- Prerequisite: Have deployment files ready.
- Deploy them as ClusterIP services:
  ```
  kubectl apply -f deploy.yaml
  ```

## Route Using YAML

In this section, use paths...
```

Please note that in Markdown, you need to properly format code blocks by using triple backticks (```) before and after the code. Also, ensure that you maintain the appropriate indentation within the code blocks to preserve the formatting.