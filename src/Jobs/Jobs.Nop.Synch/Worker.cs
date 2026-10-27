using Jobs.Nop.Synch.Processors;
using Microsoft.Extensions.Hosting;

namespace Jobs.Nop.Synch;

public class Worker : BackgroundService
{
    private readonly ICategoryProcessor _categoryProcessor;
    private readonly IAttributeProcessor _attributeProcessor;
    private readonly IProductProcessor _productProcessor;

    public Worker(ICategoryProcessor categoryProcessor, IAttributeProcessor attributeProcessor, IProductProcessor productProcessor)
    {
        _productProcessor = productProcessor;
        _categoryProcessor = categoryProcessor;
        _attributeProcessor = attributeProcessor;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //await _productProcessor.DeleteAllStocksAsync();
        //await _categoryProcessor.DeleteAllCategoriesAsync();
        //await _attributeProcessor.DeleteAllSpecificationAttributesAsync();


        await _attributeProcessor.CreateSpecificationAttributesAsync();        
        await _categoryProcessor.CreateCategoriesAsync();        
        await _productProcessor.CreateAllStocksAsync();

        /*while (!stoppingToken.IsCancellationRequested)
        {

        }*/
    }
}