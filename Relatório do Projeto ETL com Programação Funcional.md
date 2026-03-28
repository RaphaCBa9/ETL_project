# Relatório do Projeto ETL com Programação Funcional

**Aluno:** Raphael Cavalcanti Banov  
**Email:** raphaelb3@al.insper.edu.br  
**Disciplina:** Programação Funcional (Engenharia de Computação - 2026.1)

---

## 1. Introdução

Este relatório descreve o desenvolvimento e a arquitetura de um projeto de ETL (Extract, Transform, Load) construído inteiramente com o paradigma de programação funcional utilizando a linguagem F#. O objetivo central do projeto é processar dados provenientes de dois arquivos CSV (pedidos e itens de pedidos), aplicar transformações funcionais puras e gerar arquivos de saída contendo valores agregados.

A escolha da linguagem F# e do paradigma funcional mostra-se particularmente adequada para processos de ETL. A utilização de funções puras, imutabilidade e funções de alta ordem (como `map`, `filter` e `fold`) garante um processamento de dados previsível, testável e livre de efeitos colaterais na etapa de transformação.

## 2. Arquitetura do Sistema

O sistema foi arquitetado respeitando a separação estrita entre funções puras (lógica de negócio) e funções impuras (operações de entrada e saída). O código-fonte foi estruturado em um único arquivo de script (`etl_project.fsx`) para facilitar a execução, dividido nas seguintes seções:

### 2.1 Tipos de Dados (Records)

Foram definidos *Records* principais para modelar o domínio da aplicação, garantindo imutabilidade por padrão:

- `Order`: Representa um pedido, contendo `id`, `client_id`, `order_date`, `status` e `origin`.
- `OrderItem`: Representa um item de pedido, contendo `order_id`, `product_id`, `quantity`, `price` e `tax`.
- `OrderSummary`: Representa a saída processada primária, contendo `order_id`, `total_amount` e `total_taxes`.
- `MonthlySummary`: Representa as estatísticas agregadas por mês e ano.

### 2.2 Helper Functions (Funções Auxiliares)

Para a etapa de extração, foram implementadas funções auxiliares responsáveis por realizar o *parsing* seguro das strings provenientes dos arquivos CSV. Estas funções utilizam o tipo `option` do F# para lidar com possíveis falhas de conversão de forma elegante, sem lançar exceções.

As funções `lineToOrder` e `lineToOrderItem` atuam como conversores, transformando linhas de texto bruto nos respectivos *Records* fortemente tipados.

### 2.3 Funções Puras de Transformação

O núcleo do processamento ETL reside nas funções de transformação. Esta etapa é completamente pura, não dependendo de estado externo nem realizando operações de I/O. As principais operações funcionais utilizadas foram:

- **Filter**: A função `filterOrdersByStatusAndOrigin` aplica filtros opcionais de *status* e *origem* aos pedidos, permitindo a parametrização exigida pelo gestor.
- **Map**: Utilizado extensivamente para projetar dados, como na conversão de itens individuais para seus respectivos valores de receita e imposto.
- **Fold**: A função `aggregateOrderTotals` utiliza `List.fold` para acumular os valores totais (`total_amount` e `total_taxes`) de todos os itens pertencentes a um mesmo pedido.

Adicionalmente, foi implementada uma função `innerJoinOrdersAndItems` que realiza a junção entre pedidos e itens em memória, cruzando os dados através do campo `order_id`.

### 2.4 Funções Impuras (I/O)

As operações que interagem com o sistema de arquivos foram isoladas na seção de I/O. As funções `loadOrders` e `loadOrderItems` encapsulam a leitura dos arquivos CSV, enquanto `writeResultsToCsv` e `writeMonthlySummariesToCsv` são responsáveis por persistir os resultados processados no disco. O isolamento dessas funções facilita a testabilidade do núcleo do sistema.

## 3. Requisitos Opcionais Implementados

Além dos requisitos obrigatórios, foram implementados os seguintes requisitos opcionais:

### 3.1 Documentação via Docstrings (Requisito Opcional 5)

Todas as funções, tipos e módulos do script `etl_project.fsx` foram exaustivamente documentados utilizando o formato de *XML Docstrings* padrão do F# (tags `///`). A documentação inclui:
- `<summary>`: Descrição concisa do propósito da função ou tipo.
- `<param>`: Explicação detalhada de cada parâmetro de entrada.
- `<returns>`: Descrição do valor de retorno.
- `<remarks>`: Observações adicionais sobre o comportamento, pureza ou lógica interna da função.

### 3.2 Agregação Mensal e Anual (Requisito Opcional 6)

Foi implementada uma saída adicional que calcula a média de receita e impostos pagos agrupados por mês e ano.
- **Função**: `calculateMonthlySummaries` agrupa os pedidos por `(Ano, Mês)` utilizando `List.groupBy` e calcula as médias.
- **Saída**: Os dados são salvos automaticamente no arquivo `monthly_summary.csv` durante a execução do pipeline principal.
- **Estrutura**: O CSV gerado contém os campos `year-month`, `average_amount`, `average_taxes` e `order_count`.

### 3.3 Testes Completos para Funções Puras (Requisito Opcional 7)

Foi criado um arquivo dedicado exclusivamente aos testes das funções puras (`etl_tests.fsx`). O arquivo implementa um framework de testes minimalista em F# e cobre os seguintes cenários:
- **Parsing**: Validação das funções de conversão de tipos e extração de CSV.
- **Cálculos**: Verificação das regras de negócio de receita e impostos.
- **Filtros**: Testes exaustivos da lógica de filtragem com diferentes combinações (com/sem filtros, case-insensitivity).
- **Join e Agregação**: Validação do Inner Join e das totalizações.
- **Edge Cases**: Tratamento de listas vazias, itens com quantidade zero e impostos zerados.

Os testes garantem a robustez e a corretude do núcleo do sistema sem depender de frameworks externos.

## 4. Fluxo de Execução (Pipeline)

O pipeline principal do ETL demonstra a elegância da composição funcional através do operador *pipe* (`|>`):

1. Os pedidos são inicialmente filtrados com base nos parâmetros fornecidos.
2. Ocorre o *Inner Join* entre os pedidos filtrados e todos os itens.
3. Os dados combinados são agrupados por pedido e agregados para calcular os totais (`processETL`).
4. Paralelamente, as agregações mensais são calculadas a partir dos resultados primários (`calculateMonthlySummaries`).
5. Os resultados finais são exportados para os respectivos arquivos CSV.

## 5. Instruções de Execução

### 5.1 Executando o Pipeline ETL Principal

Para processar todos os pedidos sem aplicar filtros:
```bash
dotnet fsi etl_project.fsx
```

Para aplicar filtros, os parâmetros devem ser passados em ordem: `[status] [origin]`.
Exemplo filtrando por pedidos completos e origem online:
```bash
dotnet fsi etl_project.fsx Complete O
```

Os resultados serão salvos nos arquivos `output.csv` e `monthly_summary.csv`.

### 5.2 Executando os Testes

Para rodar a suíte de testes automatizados e validar as funções puras:
```bash
dotnet fsi etl_tests.fsx
```
O console exibirá o status de cada teste individual e um resumo final indicando o sucesso da execução.

## 6. Requisitos implementados

### Obrigatórios
- [x] Requisito Obrigatório 1: Implementação de funções puras para transformação de dados.
- [x] Requisito Obrigatório 2: Isolamento de funções impuras para operações de I/O.
- [x] Requisito Obrigatório 3: Utilização de funções de alta ordem (`map`, `filter`, `fold`) para processamento de listas.
- [x] Requisito Obrigatório 4: Implementação de um pipeline de processamento utilizando o operador *pipe* (`|>`).
### Opcionais
- [x] Requisito Opcional 5: Documentação completa utilizando *Docstrings*.
- [x] Requisito Opcional 6: Agregação mensal e anual de receita e impostos.
- [x] Requisito Opcional 7: Implementação de testes automatizados para funções puras.
