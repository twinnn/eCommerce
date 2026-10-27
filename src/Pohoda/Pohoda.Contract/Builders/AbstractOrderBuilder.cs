//using Common.Contract.Builders;

//namespace Pohoda.Contract.Builders
//{
//    public abstract class AbstractOrderBuilder<Data> : IBuilder<Data, CreateOrderRequest.dataPack> where Data : class
//    {
//        private const decimal Version = 2.0m;
//        private const int Ico = 53870441;
//        private const string Id = "zas001";
//        private const string PaymentType = "Plat.kartou";
//        private const string RoundingDocument = "none";
//        private const string OrderType = "receivedOrder";
//        private const string Note = "Export";
//        private const string Application = "StwTest";
//        private const string EvidentiaryResourcesMoss = "A";
//        private const string TypeServiceMoss = "GD";
//        private const string OrderText = "Amazon objednavka c. ";

//        public CreateOrderRequest.dataPack Build(Data data)
//        {
//            return new CreateOrderRequest.dataPack
//            {
//                version = Version,
//                ico = Ico,
//                note = Note,
//                id = Id,
//                application = Application,
//                dataPackItem = new CreateOrderRequest.dataPackDataPackItem
//                {
//                    id = Id,
//                    version = Version,
//                    order = new CreateOrderRequest.order
//                    {
//                        version = Version,
//                        orderHeader = new CreateOrderRequest.orderOrderHeader
//                        {
//                            orderType = OrderType,
//                            paymentType = new CreateOrderRequest.orderOrderHeaderPaymentType
//                            {
//                                ids = PaymentType
//                            },
//                            evidentiaryResourcesMOSS =
//                                new CreateOrderRequest.orderOrderHeaderEvidentiaryResourcesMOSS
//                                {
//                                    ids = EvidentiaryResourcesMoss
//                                },
//                            isDelivered = false,
//                            partnerIdentity = new CreateOrderRequest.orderOrderHeaderPartnerIdentity
//                            {
//                                address = new CreateOrderRequest.address
//                                {
//                                    country = new CreateOrderRequest.addressCountry()
//                                }
//                            },
//                            MOSS = new CreateOrderRequest.orderOrderHeaderMOSS(),
//                            carrier = new CreateOrderRequest.orderOrderHeaderCarrier(),
//                            number = new CreateOrderRequest.orderOrderHeaderNumber()
//                        },
//                        orderSummary = new CreateOrderRequest.orderOrderSummary
//                        {
//                            roundingDocument = RoundingDocument
//                        }
//                    }
//                }
//            };
//        }

//        public abstract void BuildOrderHeader(Data data);
//        public abstract void BuildOrderHeaderCarrier(Data data);
//        public abstract void BuildOrderHeaderEvidentiaryResourcesMOSS(Data data);
//        public abstract void BuildOrderHeaderMOSS(Data data);
//        public abstract void BuildOrderHeaderPartnerIdentity(Data data);
//        public abstract void BuildOrderHeaderPaymentType(Data data);
//        public abstract void BuildOrderHeaderPriceLevel(Data data);
//        public abstract void BuildOrderItem(Data data);
//        public abstract void BuildOrderItemHomeCurrency(Data data);
//        public abstract void BuildOrderItemTypeServiceMOSS(Data data);
//    }
//}