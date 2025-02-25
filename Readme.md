# Documentação do Sistema de Terreno Infinito

Este projeto implementa um sistema de geração procedural de terreno infinito usando Unity. O sistema gera e gerencia chunks de terreno dinamicamente conforme o jogador se desloca pelo mundo, aplicando diferentes biomas e detalhes visuais (como texturas, grama e objetos decorativos) de maneira eficiente e modular.

---

## Índice

- [Visão Geral](#visão-geral)
- [Estruturas de Dados e Enumeradores](#estruturas-de-dados-e-enumeradores)
- [Principais Variáveis e Configurações](#principais-variáveis-e-configurações)
- [Fluxo de Execução](#fluxo-de-execução)
- [Descrição dos Métodos](#descrição-dos-métodos)
- [Integração com Outros Sistemas](#integração-com-outros-sistemas)
- [Considerações Finais](#considerações-finais)

---

## Visão Geral

O sistema de **Terreno Infinito** é responsável por:

- **Geração Procedural:** Utiliza Perlin noise para criar um heightmap realista para cada chunk do terreno.
- **Blending de Biomas:** Mistura diferentes biomas (Deserto, Floresta, Tundra) com base na posição do jogador e ruídos de Perlin, aplicando diferentes camadas de texturas.
- **Detalhamento:** Aplica detalhes de grama (usando mesh ou billboard) e realiza o spawn de objetos decorativos (árvores, rochas, etc.) para aumentar o realismo.
- **Gestão de Chunks:** Carrega e descarrega chunks dinamicamente com base na posição do jogador, mantendo um limite máximo de chunks ativos para garantir a performance.

---

## Estruturas de Dados e Enumeradores

### Enumeradores

- **GrassRenderMode**
  - `Mesh` – Renderiza a grama utilizando um prefab 3D.
  - `Billboard2D` – Renderiza a grama utilizando uma textura 2D.

- **BiomeType**
  - `Deserto`
  - `Floresta`
  - `Tundra`

Esses enumeradores facilitam a configuração dos modos de renderização da grama e dos tipos de biomas disponíveis.

---

## Principais Variáveis e Configurações

O script está organizado em seções claramente definidas, permitindo fácil ajuste dos parâmetros via Inspector:

### Referências

- **`player`**  
  Referência ao objeto do jogador, utilizada para determinar a posição e os chunks ativos.

### Configurações do Terreno

- **`chunkSize`**  
  Tamanho de cada chunk em unidades do mundo.

- **`terrainResolution`**  
  Resolução do heightmap (formato: (2^n) + 1).

- **`terrainHeight`**  
  Altura máxima do terreno.

- **`seed`**  
  Semente para a geração procedural, garantindo variações consistentes.

### Configurações do Perlin Noise (Fallback)

- **`highFrequencyScale` e `highFrequencyAmplitude`**  
  Parâmetros para ruído de alta frequência.

- **`lowFrequencyScale` e `lowFrequencyAmplitude`**  
  Parâmetros para ruído de baixa frequência.

### Configurações dos Biomas

- **`biomeNoiseScale`**  
  Escala utilizada para a determinação dos biomas via Perlin noise.

- **`biomeDefinitions`**  
  Array que contém as definições dos biomas, com configurações específicas para cada um.

### Distância de Renderização

- **`renderDistance`**  
  Define quantos chunks ao redor do jogador serão carregados.

### Camadas de Terreno (Procedural)

- **`terrainLayerDefinitions`**  
  Define as camadas (layers) de textura para o terreno.

### Detalhes de Grama

- **`grassDetailDefinition`**  
  Configurações para aplicação de grama em uma camada específica do terreno.

- **`detailResolution` e `detailResolutionPerPacht`**  
  Resolução e densidade do detail map.

- **Parâmetros Adicionais:**  
  `wavingGrassStrength`, `wavingGrassAmount` e `wavingGrassTint` para controlar o efeito de movimento e coloração da grama.

### Alphamap

- **`alphamapResolution`**  
  Resolução utilizada para os splatmaps (mapas de textura) do terreno.

### Limite de Chunks

- **`maxChunkCount`**  
  Número máximo de chunks ativos no ambiente.

### Spawns de Objetos

- **`objectSpawnDefinitions`**  
  Definições para o spawn de objetos decorativos (árvores, rochas, etc.).

- **`objectSpawner`**  
  Componente responsável por distribuir os objetos no terreno.

### Variáveis Internas de Controle

- **`terrainChunks`**  
  Dicionário que armazena os chunks ativos, indexados por suas coordenadas.

- **`chunkQueue`**  
  Fila para enfileirar os chunks que serão criados.

- **`isChunkCoroutineRunning` e `lastPlayerChunkCoord`**  
  Variáveis auxiliares para o controle da atualização dos chunks.

---

## Fluxo de Execução

1. **Detecção do Movimento do Jogador:**  
   No método `Update()`, a posição do jogador é monitorada. Quando ele muda de chunk, o sistema dispara a atualização dos chunks.

2. **Atualização dos Chunks:**  
   O método `AtualizarChunks()`:
   - Limpa entradas inválidas do dicionário de chunks.
   - Enfileira novos chunks dentro do raio de renderização definido.
   - Inicia uma coroutine para processar a fila.
   - Chama o método `LimitarChunks()` para manter o número de chunks dentro do limite.

3. **Processamento dos Chunks:**  
   A coroutine `ProcessChunkQueue()`:
   - Processa a fila de chunks a serem gerados.
   - Chama `CriarChunkAsync()` para criar cada chunk com um intervalo, evitando travamentos.

4. **Geração Procedural dos Chunks:**  
   Cada chunk é gerado com:
   - **Heightmap:** Criado por `GenerateHeights()`, que usa `ComputeBlendedHeight()` para combinar as alturas dos diferentes biomas.
   - **Splatmap:** Gera a distribuição das texturas com base nas camadas do bioma predominante.
   - **Detalhamento:** Aplica detalhes de grama e, opcionalmente, realiza o spawn de objetos decorativos.

---

## Descrição dos Métodos

### `Update()`

- **Objetivo:**  
  Monitorar a posição do jogador e atualizar os chunks quando ele se desloca para um novo chunk.

- **Funcionamento:**  
  Calcula a coordenada do chunk atual e, se houver mudança, chama `AtualizarChunks()`.

---

### `AtualizarChunks()`

- **Objetivo:**  
  Gerenciar os chunks visíveis ao jogador.

- **Funcionamento:**  
  - Remove chunks nulos.
  - Enfileira novos chunks dentro da área de renderização.
  - Inicia a coroutine `ProcessChunkQueue()` se necessário.
  - Chama `LimitarChunks()` para manter o limite de chunks ativos.

---

### `LimitarChunks()`

- **Objetivo:**  
  Garantir que o número de chunks ativos não exceda `maxChunkCount`.

- **Funcionamento:**  
  - Ordena os chunks pela distância ao jogador.
  - Remove e destrói os chunks mais distantes até que o número de chunks esteja dentro do limite.

---

### `ProcessChunkQueue()`

- **Objetivo:**  
  Processar a fila de chunks a serem criados.

- **Funcionamento:**  
  Executa uma corrotina que, a cada iteração, remove uma coordenada da fila e chama `CriarChunkAsync()` para gerar o chunk correspondente, com um pequeno delay entre as execuções.

---

### `CriarChunkAsync(Vector2Int coord)`

- **Objetivo:**  
  Gerar, de forma assíncrona, um chunk com todas as suas características.

- **Funcionamento:**  
  - Calcula a posição do chunk no mundo.
  - Determina o bioma predominante para aquele chunk.
  - Executa uma tarefa assíncrona que gera o heightmap e o splatmap.
  - Cria um objeto Terrain, aplica detalhes de grama e realiza o spawn de objetos decorativos (se configurado).

---

### `GenerateHeights(Vector3 offset)`

- **Objetivo:**  
  Gerar um heightmap para o chunk, levando em conta o deslocamento no mundo.

- **Funcionamento:**  
  Itera sobre uma grade de pontos com base na resolução do terreno, calcula a altura de cada ponto via `ComputeBlendedHeight()` e normaliza o valor.

---

### `ComputeBlendedHeight(float worldX, float worldZ)`

- **Objetivo:**  
  Calcular a altura em um ponto, fazendo o blending entre diferentes biomas.

- **Funcionamento:**  
  - Usa Perlin noise para determinar os pesos dos biomas.
  - Obtém as definições de cada bioma e calcula a altura individualmente com `CalcularAltura()`.
  - Retorna a altura final como uma média ponderada dos valores dos biomas.

---

### Métodos Auxiliares para Biomas

- **`GetBiomeByType(BiomeType type)`**  
  Retorna a definição do bioma correspondente ao tipo solicitado.

- **`GetBiomeAtPosition(Vector3 pos)`**  
  Utiliza Perlin noise para determinar qual bioma predomina com base na posição do chunk.

---

### `AplicarDetalhesGrama(TerrainData terrainData, float[,,] splatmapData, BiomeDefinition biome)`

- **Objetivo:**  
  Configurar os detalhes de grama no TerrainData conforme as definições e camadas do bioma.

- **Funcionamento:**  
  - Valida o índice do layer destinado à grama.
  - Configura o `DetailPrototype` de acordo com o modo de renderização selecionado (mesh ou billboard).
  - Cria e preenche o detail layer com a densidade da grama baseada no splatmap.

---

### `CalcularAltura(float worldX, float worldZ, BiomeDefinition biome)`

- **Objetivo:**  
  Calcular a altura de um ponto utilizando dois níveis de Perlin noise (alta e baixa frequência), conforme as configurações do bioma.

- **Funcionamento:**  
  Soma os valores obtidos de duas chamadas de Perlin noise, cada uma ponderada pela sua amplitude.

---

### `CalcularSlope(float[,] alturas, int x, int z)`

- **Objetivo:**  
  Calcular a inclinação (slope) de um ponto na grade de alturas.

- **Funcionamento:**  
  - Utiliza os valores dos pontos vizinhos para estimar a variação de altura.
  - Calcula a derivada central e converte o resultado em ângulos (graus) utilizando funções trigonométricas.

---

## Integração com Outros Sistemas

- **TerrainObjectSpawner:**  
  O script requer o componente `TerrainObjectSpawner` (através do atributo `[RequireComponent]`), que gerencia o spawn de objetos decorativos no terreno.

- **Definições de Bioma e Layers:**  
  Os arrays `biomeDefinitions` e `terrainLayerDefinitions` devem ser configurados via Inspector ou através de scripts auxiliares, permitindo ajustes específicos para cada bioma.

- **Detalhes de Grama e Objetos:**  
  As configurações para a aplicação de grama e spawn de objetos permitem personalizar a aparência do terreno, aumentando a imersão e a diversidade visual.

---

## Considerações Finais

Este sistema foi desenvolvido para oferecer um ambiente expansível e dinâmico, onde a geração procedural de terreno se adapta ao movimento do jogador sem comprometer a performance. A utilização de corrotinas e tarefas assíncronas permite uma execução suave, enquanto a modularidade das configurações possibilita extensões e customizações conforme as necessidades do projeto.

Esta documentação serve como referência tanto para novos desenvolvedores que desejam compreender o funcionamento interno do sistema quanto para usuários que pretendem estender ou modificar suas funcionalidades.

---
