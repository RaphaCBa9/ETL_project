# Relatório do Projeto ETL com Programação Funcional

**Aluno:** Raphael Cavalcanti Banov  
**Email:** raphaelb3@al.insper.edu.br  
**Disciplina:** Programação Funcional (Engenharia de Computação - 2026.1)

---
## 0. Disclaimer sobre uso de LLMs e Inteligência Artificial Generativa

Eu, Raphael Cavalcanti Banov, declaro e confirmo a utilização de Inteligência Artificial Generativa durante o desenvolvimento deste projeto de ETL.

O código-fonte do projeto foi planejado e desenvolvido exclusivamente por mim, Raphael Cavalcanti Banov, porém com suporte de Inteligência Artificial Generativa para:
- Revisão de sintaxe;
- Debugging;
- Autocompletar trechos de código repetitivos;
- Documentação e comentários explicativos.

Além do desenvolvimento de código, a Inteligência Artificial Generativa foi utilizada para:
- Refinar a estrutura do relatório;
- Melhorar a clareza e a organização do texto;
- Garantir a aderência às melhores práticas de escrita técnica, ortografia, coesão e coerência.


## 1. Introdução

Este relatório descreve o desenvolvimento e a arquitetura de um projeto de ETL (Extract, Transform, Load) construído inteiramente com o paradigma de programação funcional utilizando a linguagem F#. O objetivo central do projeto é processar dados provenientes de dois arquivos CSV (pedidos e itens de pedidos), aplicar transformações funcionais puras e gerar um novo arquivo CSV contendo os valores agregados e os impostos totais de cada pedido.

A escolha da linguagem F# e do paradigma funcional mostra-se particularmente adequada para processos de ETL. A utilização de funções puras, imutabilidade e funções de alta ordem (como `map`, `filter` e `fold`) garante um processamento de dados previsível, testável e livre de efeitos colaterais na etapa de transformação.

## 2. Arquitetura do Sistema

O sistema foi arquitetado respeitando a separação estrita entre funções puras (lógica de negócio) e funções impuras (operações de entrada e saída). O código-fonte foi estruturado em um único arquivo de script (`etl_project.fsx`) para facilitar a execução, dividido nas seguintes seções:

### 2.1 Tipos de Dados (Records)

Foram definidos três *Records* principais para modelar o domínio da aplicação, garantindo imutabilidade por padrão:

- `Order`: Representa um pedido, contendo `id`, `client_id`, `order_date`, `status` e `origin`.
- `OrderItem`: Representa um item de pedido, contendo `order_id`, `product_id`, `quantity`, `price` e `tax`.
- `OrderSummary`: Representa a saída processada, contendo `order_id`, `total_amount` e `total_taxes`.

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

As operações que interagem com o sistema de arquivos foram isoladas na seção de I/O. As funções `loadOrders` e `loadOrderItems` encapsulam a leitura dos arquivos CSV, enquanto `writeResultsToCsv` é responsável por persistir o resultado processado no disco. O isolamento dessas funções facilita a testabilidade do núcleo do sistema.

## 3. Fluxo de Execução (Pipeline)

O pipeline principal do ETL, encapsulado na função `processETL`, demonstra a elegância da composição funcional através do operador *pipe* (`|>`):

1. Os pedidos são inicialmente filtrados com base nos parâmetros fornecidos.
2. Ocorre o *Inner Join* entre os pedidos filtrados e todos os itens.
3. Os dados combinados são agrupados por pedido e agregados para calcular os totais.
4. O resultado final é ordenado pelo identificador do pedido para facilitar a visualização.

## 4. Instruções de Execução

O projeto foi desenvolvido como um script F# (`.fsx`) e pode ser executado utilizando a ferramenta de linha de comando `dotnet fsi`.

### 4.1 Pré-requisitos

- .NET SDK instalado (versão 8.0 ou superior recomendada).
- Arquivos `order.csv` e `order_item.csv` presentes no mesmo diretório do script.

### 4.2 Executando o Script

Para processar todos os pedidos sem aplicar filtros:
```bash
dotnet fsi etl_project.fsx
```

Para aplicar filtros, os parâmetros devem ser passados em ordem: `[status] [origin]`.
Exemplo filtrando por pedidos completos e origem online:
```bash
dotnet fsi etl_project.fsx Complete O
```

Exemplo filtrando apenas por status pendente:
```bash
dotnet fsi etl_project.fsx Pending
```

O resultado será salvo no arquivo `output.csv` no mesmo diretório.

## 5. Conclusão

O projeto atende a todos os requisitos obrigatórios estabelecidos. A utilização de F# demonstrou como o paradigma funcional simplifica a implementação de pipelines de processamento de dados. A separação clara entre funções puras e impuras resultou em um código limpo, de fácil manutenção e aderente às melhores práticas de engenharia de software. O sistema está preparado para, futuramente, incorporar os requisitos opcionais propostos.
